using System.Data.Common;
using System.Text;
using Azure.Identity;
using FluentValidation;
using HIS.Api.Middleware;
using HIS.Application;
using HIS.Infrastructure;
using HIS.Shared.Context;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Secrets (production) ----
// Azure Key Vault is added as a configuration source when "KeyVault:Uri" is set
// (typically only in prod). Identity is DefaultAzureCredential — managed identity in
// Azure, az-cli / env vars locally. Secret names map '--' → ':' (e.g. a secret named
// "ConnectionStrings--Platform" overrides ConnectionStrings:Platform). Env vars using
// the "Section__Key" convention also override, so a vault is optional.
var keyVaultUri = builder.Configuration["KeyVault:Uri"];
if (!string.IsNullOrWhiteSpace(keyVaultUri))
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());

// ---- Services ----
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Trust X-Forwarded-* from a reverse proxy / ingress that terminates TLS, so HTTPS
// detection + redirects are correct behind App Service / nginx / a load balancer.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

// Resolve the provisioning template root to an absolute path. Configured value may be
// relative to the repo root (src/HIS.Api → ../../). Absolute values are kept as-is.
var templateRoot = builder.Configuration["Provisioning:TemplateRoot"];
if (!string.IsNullOrWhiteSpace(templateRoot) && !Path.IsPathRooted(templateRoot))
    builder.Configuration["Provisioning:TemplateRoot"] =
        Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", templateRoot));

builder.Services.AddApplication();        // MediatR + CQRS behaviors + validators
builder.Services.AddInfrastructure();     // Dapper repositories (config-driven connection string)
builder.Services.AddSignalR();            // real-time hubs (queue boards / signage, task 0.9)

// Per-request branch/user context (SRS §3.21). Populated by BranchContextMiddleware.
builder.Services.AddScoped<IBranchContext, BranchContext>();

// Per-request tenant routing (L1.6). Populated by TenantResolutionMiddleware.
builder.Services.AddScoped<ITenantContext, TenantContext>();

// L1.2 — ensure a platform superadmin exists on startup (config-driven bootstrap).
builder.Services.AddHostedService<HIS.Api.Startup.SuperAdminSeeder>();

// JWT auth — all parameters come from config (SRS §8.1, nothing hardcoded).
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"];
if (!string.IsNullOrWhiteSpace(signingKey))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            // Keep our short claim names ("role", "name", …) intact — don't remap to long URIs.
            o.MapInboundClaims = false;
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt["Issuer"],
                ValidAudience = jwt["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                NameClaimType = "name",
                RoleClaimType = "role"
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
app.UseForwardedHeaders();

// TLS in transit (SRS §8.1). In non-dev, enforce HTTPS + HSTS. When TLS is terminated
// at a proxy the ForwardedHeaders above make this a no-op for already-secure requests.
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

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
    catch (System.Security.Authentication.AuthenticationException aex)
    {
        // RBAC: caller not authenticated (L1.2.6).
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ctx.Response.WriteAsJsonAsync(new { title = aex.Message });
    }
    catch (UnauthorizedAccessException uex)
    {
        // RBAC: authenticated but lacks the required permission (L1.2.6).
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        await ctx.Response.WriteAsJsonAsync(new { title = uex.Message });
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
app.UseMiddleware<TenantResolutionMiddleware>();

app.MapControllers();
app.MapHub<HIS.Api.RealTime.QueueHub>("/hubs/queue");     // real-time queue board (task 0.9)
app.MapHub<HIS.Api.RealTime.AlertsHub>("/hubs/alerts");  // hospital-wide emergency alerts (task 0.9)
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));

app.Run();
