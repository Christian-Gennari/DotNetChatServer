using ChatServer.Store;
using Shared;
using ChatServer.Logger;

namespace ChatServer.Endpoints;

public static class AuthEndpoints
{
  public static RouteGroupBuilder MapAuthEndpoints(
      this IEndpointRouteBuilder app
      )
  {
    var auth = app.MapGroup("/auth").WithTags("Authentication");

    #region LOGIN
    auth.MapPost("/login", (UserStore userStore, UserDTO dto) =>
    {
      ServerLog.Info($"Login attempt for '{dto.Username}'");

      // 400: invalid request shape
      if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
      {
        ServerLog.Warning("Login failed: missing username or password");
        return Results.BadRequest();
      }

      var user = userStore.GetByUsername(dto.Username);

      // 401: credentials invalid
      if (user == null || user.Password != dto.Password)
      {
        ServerLog.Warning($"Login failed (invalid credentials) for '{dto.Username}'");
        return Results.Unauthorized();
      }

      // Update the auth token
      var token = userStore.AssignNewSessionAuthToken(user);

      ServerLog.Success($"User '{dto.Username}' logged in");

      var response = new LoginResponseDTO
      {
        Username = user.Username,
        Token = token
      };

      // 200: success
      return Results.Ok(response);
    })
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.WithSummary("User Login")
.WithDescription(
    "Authenticates a user based on the provided username and password. " +
    "A valid login returns `200` with the caller’s username and a newly issued session token. " +
    "Incorrect credentials lead to `401`, and requests missing required fields result in `400`. " +
    "The session token identifies the caller in future authenticated requests."
);
    #endregion

    #region REGISTER
    auth.MapPost("/register", (UserStore userStore, UserDTO dto) =>
    {
      ServerLog.Info($"Registration attempt for '{dto.Username}'");

      // 400: invalid request shape
      if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
      {
        ServerLog.Warning("Registration failed: missing username or password");
        return Results.BadRequest();
      }

      // 409: username already exists
      if (!userStore.Add(dto.Username, dto.Password))
      {
        ServerLog.Warning($"Registration failed: username '{dto.Username}' already exists");
        return Results.Conflict();
      }

      // Just in case something unexpected happened
      var newUser = userStore.GetByUsername(dto.Username);
      if (newUser == null)
      {
        ServerLog.Error($"User '{dto.Username}' was added but could not be retrieved immediately after creation");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
      }

      ServerLog.Success($"User '{dto.Username}' registered");

      // 201: created, return the username
      return Results.Created("", newUser.Username);
    })
.Produces(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status409Conflict)
.Produces(StatusCodes.Status500InternalServerError)
.WithSummary("Register User Account")
.WithDescription(
    "Creates a new user account for the caller. " +
    "A successful registration produces `201` with the created username. " +
    "Conflicting usernames result in `409`, and requests missing required fields lead to `400`. " +
    "Unexpected retrieval issues during the creation process return `500`."
);
    #endregion

    return auth;
  }
}
