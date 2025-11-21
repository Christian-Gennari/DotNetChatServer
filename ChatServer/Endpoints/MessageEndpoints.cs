using ChatServer.Store;
using ChatServer.Auth;
using Shared;
using Scalar.AspNetCore;
using ChatServer.Logger;
using ChatServer.Services;

namespace ChatServer.Endpoints;

public static class MessageEndpoints
{
  public static RouteGroupBuilder MapMessageEndpoints(
      this IEndpointRouteBuilder app)
  {
    var messages = app.MapGroup("/messages").WithTags("Messages");

    #region SEND MESSAGE
    messages.MapPost("/send", (HttpContext context, UserStore userStore, MessageStore messageStore, MessageDTO dto) =>
    {
      // 401: authentication required
      if (!AuthUtils.TryAuthenticate(context.Request, userStore, out var caller) || caller == null)
      {
        ServerLog.Warning("Unauthorized attempt to send a message"); // TODO: could be improved to add more details, like token and IP address
        return Results.Unauthorized();
      }

      // 400: missing sender or content
      if (string.IsNullOrWhiteSpace(dto.Content) || string.IsNullOrWhiteSpace(dto.Sender))
      {
        ServerLog.Warning($"User '{caller.Username}' sent invalid message data (empty sender or content)");
        return Results.BadRequest();
      }

      // 403: sender must match authenticated user
      if (!AuthRules.IsSelf(caller, dto.Sender))
      {
        ServerLog.Warning($"User '{caller.Username}' attempted to send a message as '{dto.Sender}'");
        return Results.StatusCode(StatusCodes.Status403Forbidden);
      }

      // Attempt to store the message
      var added = messageStore.Add(dto.Sender, dto.Content);

      // 500: unexpected store failure
      if (!added)
      {
        ServerLog.Error(
            $"Message storage failed for user '{dto.Sender}'"
        );
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
      }

      ServerLog.Info($"Message stored from '{dto.Sender}'");

      // 204: success
      return Results.NoContent();
    })
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status500InternalServerError)
.WithSummary("Send Message")
.WithDescription(
    "Processes a new chat message from an authenticated caller. " +
    "A valid request stores the message and returns `204` to signal completion. " +
    "Missing fields result in `400`, unauthenticated calls receive `401`, and a sender mismatch triggers `403`. " +
    "Unexpected storage problems lead to `500`."
)
.WithBadge("Auth Required 🔐", BadgePosition.Before, "#ffec72");
    #endregion

    #region GET MESSAGE UPDATES (LONG POLLING)
    messages.MapGet("/updates", async (
        HttpContext context,
        UserStore userStore,
        MessageStore messageStore,
        MessageNotifier notifier,
        int lastId) =>
    {
      // 401: authentication required
      if (!AuthUtils.TryAuthenticate(context.Request, userStore, out var caller) || caller == null)
      {
        ServerLog.Warning("Unauthorized attempt to poll message updates");
        return Results.Unauthorized();
      }

      // 400: invalid lastId
      if (lastId < 0)
      {
        ServerLog.Warning($"User '{caller.Username}' sent invalid lastId value '{lastId}' in update request");
        return Results.BadRequest();
      }

      // 1. Immediate updates
      var updates = messageStore.GetMessagesAfter(lastId);
      if (updates.Count > 0)
      {
        ServerLog.Trace($"Immediate updates returned for '{caller.Username}' ({updates.Count} messages)");

        var dtoUpdates = updates.Select(m => new MessageDTO
        {
          Id = m.Id,
          Sender = m.Sender,
          Content = m.Content,
          Timestamp = m.Timestamp
        }).ToList();

        return Results.Ok(dtoUpdates);
      }

      // LOG: No immediate updates, waiting
      ServerLog.Trace($"No immediate updates for '{caller.Username}'. Entering wait state...");

      // 2. Wait for notification or timeout
      var waitTask = notifier.WaitForNextMessageAsync();
      var timeoutTask = Task.Delay(25000);

      var completed = await Task.WhenAny(waitTask, timeoutTask);

      if (completed == timeoutTask)
      {
        ServerLog.Warning($"Long-poll timeout for '{caller.Username}' (25 seconds, no new messages)");
        return Results.Ok(new List<MessageDTO>());
      }

      // LOG: Notified by MessageNotifier
      ServerLog.Trace($"Notifier woke long-poll request for '{caller.Username}'");

      // 3. Fetch updates again after being signaled (if timeout was not reached before signaling)
      updates = messageStore.GetMessagesAfter(lastId);

      var dtoUpdatesAfterNotify = updates.Select(m => new MessageDTO
      {
        Id = m.Id,
        Sender = m.Sender,
        Content = m.Content,
        Timestamp = m.Timestamp
      }).ToList();

      // LOG: Returning updates after notifier signal
      ServerLog.Info($"Long-poll response for '{caller.Username}': {dtoUpdatesAfterNotify.Count} new messages");

      return Results.Ok(dtoUpdatesAfterNotify);
    })
.Produces<IEnumerable<MessageDTO>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.WithSummary("Get Message Updates (Long Polling)")
.WithDescription(
    "Retrieves new messages created after the caller’s last received ID. " +
    "If new messages exist, they are returned immediately with `200`. " +
    "If no messages are available, the request waits until a new message arrives or until the 25 second timeout passes. " +
    "The notifier resumes the request as soon as a new message is posted, which creates efficient long polling without repeated checks."
)
.WithBadge("Auth Required 🔐", BadgePosition.Before, "#ffec72");
    #endregion

    #region GET MESSAGE HISTORY (WITH OPTIONAL TAKE PARAMETER)
    messages.MapGet("/history", (HttpContext context, UserStore userStore, MessageStore messageStore, int? take) =>
    {
      // 401: authentication required
      if (!AuthUtils.TryAuthenticate(context.Request, userStore, out var caller) || caller == null)
      {
        ServerLog.Warning("Unauthorized attempt to fetch message history");
        return Results.Unauthorized();
      }

      // 400: invalid query parameter
      if (take.HasValue && take.Value <= 0)
      {
        ServerLog.Warning($"User '{caller.Username}' provided invalid take value '{take}'");
        return Results.BadRequest();
      }

      var messages = take.HasValue
          ? messageStore.GetLast(take.Value)
          : messageStore.GetAll();

      // 200: always OK, returns empty list if no messages exist
      ServerLog.Info($"Message history requested by '{caller.Username}' with {messages.Count} messages returned");
      return Results.Ok(messages);
    })
.Produces<IEnumerable<MessageDTO>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.WithSummary("Get Message History")
.WithDescription(
    "Returns stored chat messages for an authenticated caller. " +
    "A valid request produces `200` with the entire history or a limited set when the take parameter is provided. " +
    "Invalid values for the parameter result in `400`, and unauthorized requests receive `401`."
)
.WithBadge("Auth Required 🔐", BadgePosition.Before, "#ffec72");
    #endregion

    #region CLEAR MESSAGE HISTORY
    messages.MapPost("/clear", (HttpContext context, UserStore userStore, MessageStore messageStore) =>
    {
      // 401: authentication required
      if (!AuthUtils.TryAuthenticate(context.Request, userStore, out var caller) || caller == null)
      {
        ServerLog.Warning("Unauthorized attempt to clear message history");
        return Results.Unauthorized();
      }

      // 403: authorization, admin-only operation
      if (!caller.IsAdmin)
      {
        ServerLog.Warning($"User '{caller.Username}' attempted to clear message history without admin privileges");
        return Results.StatusCode(StatusCodes.Status403Forbidden);
      }

      // Attempt to clear all stored messages
      var cleared = messageStore.ClearAll();

      // 500: unexpected failure
      if (!cleared)
      {
        ServerLog.Error("Message history clear operation failed unexpectedly");
        return Results.StatusCode(StatusCodes.Status500InternalServerError);
      }

      // 204: success, no content
      ServerLog.Success($"Message history cleared by admin '{caller.Username}'");
      return Results.NoContent();
    })
.WithBadge("Danger Zone 💣", BadgePosition.Before, "#ff3b30")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status500InternalServerError)
.WithSummary("Clear Message History")
.WithDescription(
    "Removes all stored chat messages for an authenticated administrator. " +
    "A successful clear returns `204`. Unauthorized requests receive `401`, " +
    "and callers without administrative rights receive `403`. " +
    "Unexpected storage issues lead to `500`."
)
.WithBadge("Admin Only 🔐", BadgePosition.Before, "#707fff");
    #endregion

    return messages;
  }
}
