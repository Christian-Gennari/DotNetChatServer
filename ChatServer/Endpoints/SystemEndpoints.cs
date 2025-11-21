using Scalar.AspNetCore;

namespace ChatServer.Endpoints;

public static class SystemEndpoints
{
  public static RouteGroupBuilder MapSystemEndpoints(
      this IEndpointRouteBuilder app)
  {
    var system = app.MapGroup("/system").WithTags("System");

    #region HEALTH CHECK
    system.MapGet("/health", () => Results.Ok("OK"))
    .WithMetadata(new HttpMethodMetadata(["HEAD"]))
    .Produces<string>(StatusCodes.Status200OK)
    .WithSummary("Health Check")
    .WithDescription(
        "Reports the operational status of the server. " +
        "A healthy server returns `200` with the content `OK`. " +
        "Both GET and HEAD requests are supported to allow external services to monitor uptime."
    )
    .WithBadge("🩺💚", BadgePosition.After, "#e5e5e5")
    .WithBadge("⚙️ UptimeRobot", BadgePosition.After, "#51ff94");
    #endregion

    return system;
  }
}
