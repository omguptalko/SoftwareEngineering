using System.Reflection;
using FluentValidation;
using HIS.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace HIS.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers MediatR, all CQRS handlers, FluentValidation validators, and the
    /// cross-cutting pipeline behaviors (validation → logging → audit). Order matters:
    /// validation runs first, audit wraps the handler outcome.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

        return services;
    }
}
