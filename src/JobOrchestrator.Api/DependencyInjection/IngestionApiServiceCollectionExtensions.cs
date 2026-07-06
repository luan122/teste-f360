using System.Reflection;
using System.Text;
using FluentValidation;
using JobOrchestrator.Api.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace JobOrchestrator.Api.DependencyInjection;

/// <summary>Composition root for the ingestion API's host-owned concerns: controllers, authentication, and OpenAPI.</summary>
public static class IngestionApiServiceCollectionExtensions
{
    public static IServiceCollection AddIngestionApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers();
        services.AddValidatorsFromAssembly(typeof(IngestionApiServiceCollectionExtensions).Assembly);

        services.Configure<ApiKeyOptions>(configuration.GetSection(ApiKeyOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var jwtIssuer = jwtSection["Issuer"] ?? string.Empty;
        var jwtAudience = jwtSection["Audience"] ?? string.Empty;
        var jwtSigningKey = jwtSection["SigningKey"] ?? string.Empty;

        services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = ApiKeyAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = ApiKeyAuthenticationHandler.SchemeName;
            })
            .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName, _ => { })
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                        string.IsNullOrEmpty(jwtSigningKey) ? new string('0', 32) : jwtSigningKey)),
                    ValidateLifetime = true,
                };
            });

        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
                    ApiKeyAuthenticationHandler.SchemeName, JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();
        });

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Distributed Job Orchestrator — Ingestion API",
                Version = "v1",
            });

            options.AddSecurityDefinition(ApiKeyAuthenticationHandler.SchemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.ApiKey,
                In = ParameterLocation.Header,
                Name = ApiKeyAuthenticationHandler.HeaderName,
                Description = "Chave de API estática via cabeçalho X-Api-Key",
            });

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "JWT obtido via POST /auth/token",
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                            { Type = ReferenceType.SecurityScheme, Id = ApiKeyAuthenticationHandler.SchemeName }
                    },
                    Array.Empty<string>()
                }
            });

            var xmlPath = Path.Combine(AppContext.BaseDirectory,
                $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
        });

        return services;
    }
}
