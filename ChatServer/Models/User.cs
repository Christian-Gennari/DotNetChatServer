namespace ChatServer.Models;

public class User(string username, string password, bool isAdmin, string sessionAuthToken)
{
  public int Id { get; set; }
  public string Username { get; set; } = username;
  public string Password { get; set; } = password;
  public bool IsAdmin { get; set; } = isAdmin;
  public string SessionAuthToken { get; set; } = sessionAuthToken;
  
  public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

}
