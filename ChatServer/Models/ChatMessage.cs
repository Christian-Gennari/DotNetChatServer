namespace ChatServer.Models;

public class ChatMessage
{
  public int Id { get; set; }
  public int SenderUsername { get; set; }
  public string Content { get; set; } = string.Empty;
  public DateTime Timestamp { get; set; } = DateTime.UtcNow;

}
