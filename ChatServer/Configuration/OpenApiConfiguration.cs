using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;

namespace ChatServer.Configuration;

public static class OpenApiConfiguration
{
    public static void AddCustomOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            // 1. FIRST APPLY YOUR TRANSFORMER
            options.AddDocumentTransformer((document, context, ct) =>
            {
                // Ensure the document is not null
                document.Info ??= new();
                document.Components ??= new();
                document.Components.SecuritySchemes ??= new Dictionary<string, OpenApiSecurityScheme>();
                document.SecurityRequirements ??= [];

                // Configure the document
                document.Info.Title = "Chat Server API Docs";
                document.Info.Description =
                    "The Chat Server API documentation provides a comprehensive overview of every public endpoint exposed by the chat server. " +
                    "You will find detailed specifications for each route, including path parameters, query parameters, request and response payloads, status codes and headers. " +
                    "Authentication and authorization requirements are clearly indicated for each call, with examples where relevant. " +
                    "Visit the interactive API explorer section to submit test requests, inspect live responses and understand the operational behavior of endpoints in real time. " +
                    "Use this reference to plan integration, verify expected data flows, troubleshoot error conditions and ensure the client software aligns with the server’s semantics.";

                const string schemeKey = "SessionAuth";

                // Ensure this overwrites any previous default scheme
                document.Components.SecuritySchemes[schemeKey] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.ApiKey,
                    Name = "SessionAuthToken",
                    In = ParameterLocation.Header,
                    Description = "Session auth token which is normally returned in the server response when user logs in successfully (/auth/login/)."
                };

                // Apply globally
                document.SecurityRequirements.Clear();
                document.SecurityRequirements.Add(new OpenApiSecurityRequirement
                {
                    { document.Components.SecuritySchemes[schemeKey], Array.Empty<string>() }
                });

                return Task.CompletedTask;
            });

            // 2. THEN APPLY SCALAR TRANSFORMERS LAST
            options.AddScalarTransformers();
        });
    }
}
