using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using DevFlow.AspNetCore.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace DevFlow.AspNetCore.DependencyInjection;

public static class SwaggerExtensions
{
    public static IServiceCollection AddConfiguredApiVersioning(this IServiceCollection services)
    {
        services
            .AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1.0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

        return services;
    }

    public static IServiceCollection AddConfiguredSwagger(this IServiceCollection services)
    {
        services.Configure<ApiDocumentationOptions>(options =>
        {
            options.Title = "Company.Project API";
            options.Description = "Clean Architecture + self-made CQRS sender sample API";
        });

        services.AddEndpointsApiExplorer();
        services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT Authorization header using the Bearer scheme."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecuritySchemeReference("Bearer", document, externalResource: null)
                ] = []
            });

            options.OperationFilter<IntegrationApiKeyHeaderOperationFilter>();

            options.TagActionsBy(apiDescription =>
            {
                var controllerName = apiDescription.ActionDescriptor.RouteValues["controller"];
                return [controllerName ?? "Default"];
            });

            options.OrderActionsBy(apiDescription =>
            {
                var methodOrder = GetMethodOrder(apiDescription.HttpMethod);
                var tag = apiDescription.ActionDescriptor.RouteValues["controller"] ?? string.Empty;
                var path = apiDescription.RelativePath ?? string.Empty;

                return $"{tag}_{methodOrder:D2}_{path}";
            });

            options.DocumentFilter<SortSwaggerDocumentFilter>();
        });

        return services;
    }

    public static WebApplication UseConfiguredSwagger(this WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            foreach (var description in app.DescribeApiVersions())
            {
                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName);
            }

            options.RoutePrefix = "swagger";
            options.DisplayRequestDuration();
            options.EnableTryItOutByDefault();
            options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        });

        return app;
    }

    private static int GetMethodOrder(string? method)
        => method?.ToUpperInvariant() switch
        {
            "GET" => 0,
            "POST" => 1,
            "PUT" => 2,
            "PATCH" => 3,
            "DELETE" => 4,
            _ => 5
        };
}

internal sealed class ApiDocumentationOptions
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

internal sealed class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;
    private readonly ApiDocumentationOptions _options;

    public ConfigureSwaggerOptions(
        IApiVersionDescriptionProvider provider,
        IOptions<ApiDocumentationOptions> options)
    {
        _provider = provider;
        _options = options.Value;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(description.GroupName, new OpenApiInfo
            {
                Title = _options.Title,
                Version = description.ApiVersion.ToString(),
                Description = _options.Description
            });
        }

        options.CustomSchemaIds(type => type.ToString().Replace("+", ".", StringComparison.Ordinal));
    }
}

internal sealed class IntegrationApiKeyHeaderOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor controllerActionDescriptor
            || !typeof(IntegrationApiController).IsAssignableFrom(controllerActionDescriptor.ControllerTypeInfo.AsType()))
        {
            return;
        }

        operation.Parameters ??= [];

        if (operation.Parameters.Any(parameter =>
                string.Equals(parameter.Name, "X-Integration-Key", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Integration-Key",
            In = ParameterLocation.Header,
            Required = true,
            Description = "Integration API key configured in IntegrationApi:ApiKeys.",
            Schema = new OpenApiSchema
            {
                Type = JsonSchemaType.String
            }
        });
    }
}

internal sealed class SortSwaggerDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        SortTags(swaggerDoc);
        SortOperations(swaggerDoc);
        SortPaths(swaggerDoc);
    }

    private static void SortTags(OpenApiDocument swaggerDoc)
    {
        var tags = GetOperations(swaggerDoc)
            .SelectMany(operation => operation.Tags ?? Enumerable.Empty<OpenApiTagReference>())
            .Select(tag => tag.Name)
            .Where(tagName => !string.IsNullOrWhiteSpace(tagName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(tagName => new OpenApiTag { Name = tagName })
            .ToArray();

        swaggerDoc.Tags = new SortedSet<OpenApiTag>(tags, OpenApiTagNameComparer.Instance);
    }

    private static void SortOperations(OpenApiDocument swaggerDoc)
    {
        foreach (var pathItem in swaggerDoc.Paths.Values)
        {
            if (pathItem.Operations is null)
            {
                continue;
            }

            var operations = pathItem.Operations
                .OrderBy(operation => GetMethodOrder(operation.Key.Method))
                .ThenBy(operation => operation.Key.Method, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            pathItem.Operations.Clear();

            foreach (var operation in operations)
            {
                pathItem.Operations.Add(operation.Key, operation.Value);
            }
        }
    }

    private static void SortPaths(OpenApiDocument swaggerDoc)
    {
        var sortedPaths = swaggerDoc.Paths
            .OrderBy(path => GetPrimaryTagName(path.Value), StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => GetPrimaryMethodOrder(path.Value))
            .ThenBy(path => path.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var paths = new OpenApiPaths();

        foreach (var path in sortedPaths)
        {
            paths.Add(path.Key, path.Value);
        }

        swaggerDoc.Paths = paths;
    }

    private static IEnumerable<OpenApiOperation> GetOperations(OpenApiDocument swaggerDoc)
        => swaggerDoc.Paths.Values
            .SelectMany(pathItem => pathItem.Operations is null
                ? Enumerable.Empty<OpenApiOperation>()
                : pathItem.Operations.Values);

    private static string GetPrimaryTagName(IOpenApiPathItem pathItem)
        => pathItem.Operations?.Values
            .SelectMany(operation => operation.Tags ?? Enumerable.Empty<OpenApiTagReference>())
            .Select(tag => tag.Name)
            .FirstOrDefault(tagName => !string.IsNullOrWhiteSpace(tagName))
            ?? string.Empty;

    private static int GetPrimaryMethodOrder(IOpenApiPathItem pathItem)
        => pathItem.Operations is null
            ? int.MaxValue
            : pathItem.Operations.Keys
                .Select(method => GetMethodOrder(method.Method))
                .DefaultIfEmpty(int.MaxValue)
                .Min();

    private static int GetMethodOrder(string? method)
        => method?.ToUpperInvariant() switch
        {
            "GET" => 0,
            "POST" => 1,
            "PUT" => 2,
            "PATCH" => 3,
            "DELETE" => 4,
            _ => 5
        };
}

internal sealed class OpenApiTagNameComparer : IComparer<OpenApiTag>, IEqualityComparer<OpenApiTag>
{
    public static readonly OpenApiTagNameComparer Instance = new();

    public int Compare(OpenApiTag? x, OpenApiTag? y)
        => string.Compare(x?.Name, y?.Name, StringComparison.OrdinalIgnoreCase);

    public bool Equals(OpenApiTag? x, OpenApiTag? y)
        => string.Equals(x?.Name, y?.Name, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode(OpenApiTag obj)
        => StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Name ?? string.Empty);
}
