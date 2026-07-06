using FluentValidation;
using JobOrchestrator.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace JobOrchestrator.Application.DependencyInjection;

/// <summary>Registers application-layer services: MediatR dispatch, FluentValidation validators, and the validation pipeline behavior.</summary>
public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var applicationAssembly = typeof(ApplicationServiceCollectionExtensions).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
        services.AddValidatorsFromAssembly(applicationAssembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
