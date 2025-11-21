namespace Shared;

/// <summary>
/// Responsible for: transferring chat message data between client and server.
/// Data transfer object containing message ID, sender, content, and server-generated timestamp.
/// </summary>
public class MessageDTO
{
    // Properties are nullable to allow validation before sending to server
    // if any fields are null the message is invalid (not sent)

    // Message ID is generated server-side
    public int Id { get; set; }
    public string? Sender { get; set; }
    public string? Content { get; set; }

    // Timestamp is generated server-side (UTC, which client will convert to local time)
    public DateTime Timestamp { get; set; }
}
