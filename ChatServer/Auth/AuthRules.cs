using System;
using ChatServer.Models;

namespace ChatServer.Auth;

public static class AuthRules
{
  /// <summary>
  /// Determines whether the authenticated caller is acting on their own identity.
  /// </summary>
  public static bool IsSelf(User caller, string username)
  {
    if (caller == null)
      return false;

    return caller.Username.Equals(username, StringComparison.OrdinalIgnoreCase);
  }

  /// <summary>
  /// Determines whether an authenticated caller has permission to act on a target user.
  /// Authorization succeeds when the caller is the same user as the target or
  /// when the caller has administrator status.
  /// </summary>
  public static bool IsSelfOrAdmin(User caller, string targetUsername)
  {
    if (caller == null)
      return false;

    bool isSelf = caller.Username.Equals(targetUsername, StringComparison.OrdinalIgnoreCase);
    bool isAdmin = caller.IsAdmin;

    return isSelf || isAdmin;
  }

}
