using ChatServer.Models;
using ChatServer.Logger;

namespace ChatServer.Store;

public class UserStore : ConcurrentStoreBase
{
  // Dictionaries for storing and looking up users
  private readonly Dictionary<string, User> usersByUsername =
      new(StringComparer.OrdinalIgnoreCase);  // Change stringcomparer setting to ignore case sensitivity
  private readonly Dictionary<int, User> usersById = [];
  private readonly Dictionary<string, User> usersBySessionAuthToken = [];

  // Adjust this value to define how long a user remains "online" after their last recorded activity.
  // Any user whose LastSeenUtc (property) is older than this window is treated as offline.
  public static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(5);

  // Utility: determines if a user is online
  private static bool IsOnline(User user) => (DateTime.UtcNow - user.LastSeenUtc) < OnlineWindow;


  // User ID counter
  private int nextId = 1;

  #region ADD USER
  /// <summary>
  /// Adds a new user to the store. The method assigns a unique ID and registers
  /// the user in both UserStore dictionaries (by username and by ID).
  /// </summary>
  /// <param name="username">
  /// The identifier the caller wants to use when referring to the user.
  /// </param>
  /// <param name="password">
  /// The secret credential stored for authentication.
  /// </param>
  /// <returns>
  /// True when creation succeeds. False when a user with the same username already exists.
  /// </returns>
  public bool Add(string username, string password, bool isAdmin = false, string? sessionAuthToken = null)
  {
    return WithWrite(() =>
    {
      if (usersByUsername.ContainsKey(username))
      {
        ServerLog.Warning($"UserStore.Add failed: username '{username}' already exists");
        return false;
      }

      sessionAuthToken ??= Guid.NewGuid().ToString();

      User newUser = new(username, password, isAdmin, sessionAuthToken)
      {
        Id = nextId++
      };

      usersBySessionAuthToken[newUser.SessionAuthToken] = newUser;
      usersByUsername.Add(newUser.Username, newUser);
      usersById.Add(newUser.Id, newUser);

      ServerLog.Success($"User '{newUser.Username}' created (ID={newUser.Id}, Admin={newUser.IsAdmin})");
      return true;
    });
  }
  #endregion

  #region UPDATE USER
  /// <summary>
  /// Updates the username and optionally the password of an existing user.
  /// The update keeps dictionary consistency by removing the old key and inserting the new one.
  /// </summary>
  /// <param name="oldUsername">
  /// The current username used to locate the existing user.
  /// </param>
  /// <param name="newUsername">
  /// The username that replaces the old one. Must be unused by other users.
  /// </param>
  /// <param name="newPassword">
  /// A new password to assign. When null or empty, the existing password remains.
  /// </param>
  /// <returns>
  /// True when the update succeeds. False when the old username is not found or the new one is already taken.
  /// </returns>
  public bool Update(string oldUsername, string newUsername, string? newPassword = null)
  {
    return WithWrite(() =>
    {
      // Old username does not exist
      if (!usersByUsername.TryGetValue(oldUsername, out var user))
      {
        ServerLog.Warning($"UserStore.Update failed: user '{oldUsername}' not found");
        return false;
      }

      // If the username changes, make sure the new username is not already taken.
      if (oldUsername != newUsername && usersByUsername.ContainsKey(newUsername))
      {
        ServerLog.Warning(
            $"UserStore.Update failed: new username '{newUsername}' already taken"
        );
        return false;
      }

      // Remove the old username key from the dictionary. (Note: the user object still exists in memory at this point)
      usersByUsername.Remove(oldUsername);

      string oldNameForLog = user.Username;   // capture before changing

      // Update the properties on the existing User instance.
      user.Username = newUsername;
      // Only update the password if a new one was provided.
      if (!string.IsNullOrWhiteSpace(newPassword))
        user.Password = newPassword;

      // Reinsert the updated user using the new username as key.
      usersByUsername.Add(newUsername, user);

      ServerLog.Success($"User '{oldNameForLog}' updated to '{newUsername}'" + (string.IsNullOrWhiteSpace(newPassword) ? "" : " (password changed)"));
      return true;
    });
  }
  #endregion

  #region REMOVE USER (by USERNAME)
  /// <summary>
  /// Removes a user from the store using their username.
  /// Deletes the user from both dictionaries to maintain consistency.
  /// </summary>
  /// <param name="username">
  /// The username of the user that should be removed.
  /// </param>
  /// <returns>
  /// True when a user with the specified username existed and was removed. False when no such user was found.
  /// </returns>
  public bool Remove(string username)
  {
    return WithWrite(() =>
    {
      if (!usersByUsername.TryGetValue(username, out var user))
      {
        ServerLog.Warning($"UserStore.Remove failed: username '{username}' not found");
        return false;
      }

      // Remove old token mapping if it exists
      if (!string.IsNullOrWhiteSpace(user.SessionAuthToken))
        usersBySessionAuthToken.Remove(user.SessionAuthToken);

      usersByUsername.Remove(username);
      usersById.Remove(user.Id);

      ServerLog.Success($"User '{username}' removed (ID={user.Id})");
      return true;
    });
  }
  #endregion

  #region REMOVE USER (by ID)
  /// <summary>
  /// Removes a user from the store using their unique ID.
  /// Deletes the user from both dictionaries to maintain consistency.
  /// </summary>
  /// <param name="id">
  /// The ID of the user that should be removed.
  /// </param>
  /// <returns>
  /// True when a user with the specified ID existed and was removed. False when no such user was found.
  /// </returns>
  public bool Remove(int id)
  {
    return WithWrite(() =>
    {
      if (!usersById.TryGetValue(id, out var user))
      {
        ServerLog.Warning($"UserStore.RemoveById failed: user ID {id} not found");
        return false;
      }

      // Remove old token mapping if it exists
      if (!string.IsNullOrWhiteSpace(user.SessionAuthToken))
        usersBySessionAuthToken.Remove(user.SessionAuthToken);

      usersById.Remove(id);
      usersByUsername.Remove(user.Username);

      ServerLog.Success($"User '{user.Username}' removed by ID (ID={id})");
      return true;
    });
  }
  #endregion

  #region GET USER (by USERNAME)
  /// <summary>
  /// Retrieves a user based on their username.
  /// </summary>
  /// <param name="username">
  /// The username used as the lookup key.
  /// </param>
  /// <returns>
  /// The matching user when found, otherwise null.
  /// </returns>

  public User? GetByUsername(string username)
  {
    return WithRead(() =>
    {
      usersByUsername.TryGetValue(username, out var user);
      return user;
    });
  }
  #endregion

  #region GET USER (by ID)
  /// <summary>
  /// Retrieves a user based on their unique ID.
  /// </summary>
  /// <param name="id">
  /// The numeric identifier assigned to the user when created.
  /// </param>
  /// <returns>
  /// The matching user when found, otherwise null.
  /// </returns>
  public User? GetById(int id)
  {
    return WithRead(() =>
    {
      usersById.TryGetValue(id, out var user);
      return user;
    });
  }
  #endregion

  #region GET USER (by SESSION AUTH TOKEN)
  /// <summary>
  /// Retrieves the user associated with the given session authentication token.
  /// </summary>
  /// <param name="token">The session token issued at login.</param>
  /// <returns>
  /// The matching <see cref="User"/> instance if the token exists, otherwise <c>null</c>.
  /// </returns>
  public User? GetBySessionAuthToken(string token)
  {
    return WithRead(() =>
    {
      usersBySessionAuthToken.TryGetValue(token, out var user);
      return user;
    });
  }
  #endregion

  #region GET ALL USER[NAMES]
  /// <summary>
  /// Returns a collection of all usernames currently stored.
  /// The caller receives a read-only view of the keys from the username dictionary.
  /// </summary>
  /// <returns>
  /// An enumerable sequence of every username in the store.
  /// </returns>
  public IEnumerable<string> GetAllUsernames()
  {
    return WithRead(() => usersByUsername.Keys.ToArray());
  }
  #endregion

  #region GET ALL USER STATUSES
  /// <summary>
  /// Returns every user together with a isOnline bool that indicates whether they are currently online.
  /// </summary>
  /// <remarks>
  /// A user is considered online if their <c>LastActivityUtc</c>
  /// is within <c>OnlineWindow</c> (see <see cref="OnlineWindow"/>).
  /// </remarks>
  /// <returns>
  /// A sequence of (Username, Online) pairs.
  /// </returns>
  public IEnumerable<(string Username, bool Online)> GetAllUserStatuses()
  {
    return WithRead(() =>
    {
      return usersByUsername.Values
          .Select(u => (u.Username, IsOnline(u)))
          .ToArray();
    });
  }

  #endregion

  #region ASSIGN NEW SESSION AUTH TOKEN TO USER
  /// <summary>
  /// Generates a new session authentication token for the specified user
  /// and updates the internal token-to-user mapping accordingly.
  /// </summary>
  /// <param name="user">The user who is receiving a new session token.</param>
  /// <returns>
  /// The newly generated session authentication token.
  /// </returns>
  /// <remarks>
  /// Any existing token assigned to the user is removed before the new one is created.
  /// </remarks>
  public string AssignNewSessionAuthToken(User user)
  {
    return WithWrite(() =>
    {
      try
      {
        // Remove old token if it exists
        if (!string.IsNullOrWhiteSpace(user.SessionAuthToken))
          usersBySessionAuthToken.Remove(user.SessionAuthToken);

        // Generate and assign new token
        string newToken = Guid.NewGuid().ToString();
        user.SessionAuthToken = newToken;

        usersBySessionAuthToken[newToken] = user;

        // No logging needed on success
        return newToken;
      }
      catch (Exception ex)
      {
        ServerLog.Error(
            $"UserStore.AssignNewSessionAuthToken failed for user '{user.Username}': {ex}"
        );
        throw;
      }
    });
  }
  #endregion
}
