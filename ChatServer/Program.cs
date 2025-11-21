using ChatServer.Store;
using Scalar.AspNetCore;
using ChatServer.Configuration;
using ChatServer.Endpoints;
using ChatServer.Logger;
using ChatServer.Services;


var builder = WebApplication.CreateBuilder(args);
ServerLog.Info("Booting Ducklord Server");

// Dependency injections
builder.Services.AddSingleton<UserStore>();
builder.Services.AddSingleton<MessageStore>();
builder.Services.AddSingleton<MessageNotifier>();
ServerLog.Info("Dependency injections registered");

builder.Services.AddCustomOpenApi(); // Register the OpenAPI custom configuration which is found in the OpenApiConfiguration.cs Class
ServerLog.Info("OpenAPI configuration loaded");

var app = builder.Build();

// Get required DI services for Program.cs
var userStore = app.Services.GetRequiredService<UserStore>();
var messageStore = app.Services.GetRequiredService<MessageStore>();

// Seed initial users (for testing + Scalar UI authentication)
userStore.Add("Ducklord", "chatking", isAdmin: true);
userStore.Add("Scalar", "APIDOCS", isAdmin: true); // This user is used to allow Scalar UI to send test requests
userStore.Add("Heaton-Lover", "13371337", isAdmin: true);
userStore.Add("Ducktor III", "duckduck", isAdmin: true);
ServerLog.Info("Initial test users seeded");

// Print debug token
Console.WriteLine(userStore.GetByUsername("Scalar")?.SessionAuthToken ?? "Token retrieval error: user not found or no token assigned. Ask server admin to generate one");

// Run the server locally on port 5201
builder.WebHost.UseUrls("http://localhost:5201");

// Scalar and OpenAPI are open even in production now to make debugging easier
app.MapOpenApi();                // exposes /openapi/v1.json
app.MapScalarApiReference(opt => // exposes visual UI at /scalar
{
  opt.Title = "Ducklord's Server API Docs";
  opt.Theme = ScalarTheme.DeepSpace;
  opt.HideClientButton = true;
  opt.ExpandAllResponses = true;

  // Automatically pick as the active scheme
  opt.AddPreferredSecuritySchemes("SessionAuth");
  opt.AddApiKeyAuthentication("SessionAuth", apiKey =>
  {
    apiKey.Name = "SessionAuthToken";
    apiKey.Value = userStore.GetByUsername("Scalar")?.SessionAuthToken
                   ?? "Token retrieval error: user not found or no token assigned. Ask server admin to generate one";
  });
});

// Register all API endpoint modules
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapMessageEndpoints();
app.MapSystemEndpoints();
ServerLog.Info("All API endpoints mapped");


// Redirect root && /docs → /scalar/ (OpenAPI UI)
app.MapGet("/", () => Results.Redirect("/scalar/", permanent: false)).ExcludeFromApiReference();
app.MapGet("/docs", () => Results.Redirect("/scalar/", permanent: false)).ExcludeFromApiReference();

// Final ready state
ServerLog.Success("Ducklord Server is now running");

app.Run();

