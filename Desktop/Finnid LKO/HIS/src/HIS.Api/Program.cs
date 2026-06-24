using System.Data.Common;
using System.Text;
using FluentValidation;
using HIS.Api.Middleware;
using HIS.Application;
using HIS.Infrastructure;
using HIS.Shared.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Services ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddApplication();        // MediatR + CQRS behaviors + validators
builder.Services.AddInfrastructure();     // Dapper repositories (config-driven connection string)

// Per-request branch/user context (SRS §3.21). Populated by BranchContextMiddleware.
builder.Services.AddScoped<IBranchContext, BranchContext>();

// JWT auth — all parameters come from config (SRS §8.1, nothing hardcoded).
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"];
if (!string.IsNullOrWhiteSpace(signingKey))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt["Issuer"],
                ValidAudience = jwt["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey))
            };
        });
    builder.Services.AddAuthorization();
}

// CORS for the wireframe origin(s) — origins come from config, never hardcoded.
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    if (corsOrigins.Length > 0) p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

// ---- Pipeline ----
// Map FluentValidation failures to 400 and domain conflicts to 409 (no business logic here).
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (ValidationException vex)
    {
        ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
        await ctx.Response.WriteAsJsonAsync(new
        {
            title = "Validation failed",
            errors = vex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })
        });
    }
    catch (InvalidOperationException iex)
    {
        ctx.Response.StatusCode = StatusCodes.Status409Conflict;
        await ctx.Response.WriteAsJsonAsync(new { title = iex.Message });
    }
    catch (DbException)
    {
        // FK / unique / check constraint violations → 409 (don't leak schema detail).
        ctx.Response.StatusCode = StatusCodes.Status409Conflict;
        await ctx.Response.WriteAsJsonAsync(new { title = "Data constraint violation." });
    }
});

app.UseDefaultFiles();   // serve index.html for the wireframe
app.UseStaticFiles();
app.UseCors();
if (!string.IsNullOrWhiteSpace(signingKey))
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.UseMiddleware<BranchContextMiddleware>();

app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));

app.Run();
