namespace Shared;

/// <summary>
/// Responsible for: transferring user online/offline status information between client and server.
/// Data transfer object used for real-time user presence tracking in the chat application.
/// </summary>
public class UserStatusDTO
{
  public string Username { get; set; } = string.Empty;
  public bool Online { get; set; }
}
