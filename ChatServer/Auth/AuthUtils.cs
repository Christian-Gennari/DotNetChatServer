using ChatServer.Models;
using ChatServer.Store;

namespace ChatServer.Auth;

public static class AuthUtils
{
  public static bool TryAuthenticate(HttpRequest req, UserStore userStore, out User? user)
  {
    user = null;

    if (!req.Headers.TryGetValue("SessionAuthToken", out var raw))
      return false;

    string token = raw.ToString().Trim();
    if (string.IsNullOrWhiteSpace(token))
      return false;

    user = userStore.GetBySessionAuthToken(token);
    if (user is null)
      return false;

    return true;
  }

}
