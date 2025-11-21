using ChatServer.Models;
using Shared;
using ChatServer.Logger;
using ChatServer.Services;

namespace ChatServer.Store;

public class MessageStore(UserStore userStore, MessageNotifier messageNotifier) : ConcurrentStoreBase
{
  private readonly UserStore userStore = userStore;
  private readonly MessageNotifier messageNotifier = messageNotifier;

  private readonly List<ChatMessage> messages = [];
  private readonly Dictionary<int, ChatMessage> messagesById = [];

  // Message ID counter
  private int nextMessageId = 1;

  #region ADD MESSAGE
  /// <summary>
  /// Adds a new chat message to the store.
  /// </summary>
  /// <param name="user">The user sending the message. Must not be null.</param>
  /// <param name="messageContent">The text content of the message. Must not be null or whitespace.</param>
  /// <returns>
  /// True if the message was successfully added.
  /// False if <paramref name="user"/> is null or <paramref name="messageContent"/> is invalid.
  /// </returns>
  public bool Add(string username, string messageContent)
  {
    return WithWrite(() =>
  {
    // Validate inputs
    if (string.IsNullOrEmpty(username) || string.IsNullOrWhiteSpace(messageContent))
    {
      ServerLog.Warning($"MessageStore.Add failed: invalid parameters (username='{username}')");
      return false;
    }

    var sender = userStore.GetByUsername(username);
    if (sender == null)
    {
      ServerLog.Warning($"MessageStore.Add failed: unknown user '{username}'");
      return false;
    }

    var newMessage = new ChatMessage // Create new message
    {
      Id = nextMessageId++,
      SenderUsername = sender.Id,
      Content = messageContent,
      Timestamp = DateTime.UtcNow
    };

    // Validate message ID
    if (newMessage.SenderUsername == 0)
    {
      ServerLog.Error($"MessageStore.Add failed: SenderUsername resolved to 0 for username '{username}'");
      return false;
    }

    // Store message in both list and dictionary
    messages.Add(newMessage);
    messagesById.Add(newMessage.Id, newMessage);

    // Notify all waiting long-poll clients that a new message arrived
    messageNotifier.NotifyNewMessage();
    return true;
  });
  }
  #endregion

  #region GET ALL MESSAGES
  /// <summary>
  /// Retrieves every stored chat message and returns them in the order they were added.
  /// </summary>
  /// <remarks>
  /// Each stored message is converted into a <see cref="MessageDTO"/>. The sender identifier is resolved to a username
  /// by querying the <see cref="UserStore"/>. The returned list preserves the chronological order of insertion.
  /// </remarks>
  /// <returns>
  /// A read-only list of <see cref="MessageDTO"/> representing the full chat history.
  /// </returns>
  public IReadOnlyList<MessageDTO> GetAll()
  {
    return WithRead(() =>
  {
    var result = new List<MessageDTO>();

    foreach (var m in messages)
    {
      var user = userStore.GetById(m.SenderUsername) ?? throw new InvalidOperationException(
            $"Message with ID {m.Id} references a missing user"
        );
      result.Add(new MessageDTO
      {
        Id = m.Id,
        Sender = user.Username,
        Content = m.Content,
        Timestamp = m.Timestamp
      });
    }
    return result;
  });
  }
  #endregion

  #region GET LAST (n) MESSAGES
  /// <summary>
  /// Returns the most recently stored chat messages in chronological order up to the amount specified.
  /// </summary>
  /// <param name="count">
  /// The number of messages the caller wants to retrieve. If the value is larger than the number of stored messages,
  /// all available messages are returned.
  /// </param>
  /// <returns>
  /// A read-only list of <see cref="MessageDTO"/> representing the latest messages in the store. The list contains the
  /// newest messages based on insertion order, formatted for client use.
  /// </returns>
  public IReadOnlyList<MessageDTO> GetLast(int count)
  {
    return WithRead(() =>
    {
      if (count <= 0)
        return [];

      // Determine starting index
      int startIndex = Math.Max(0, messages.Count - count);

      var result = new List<MessageDTO>(count);

      for (int i = startIndex; i < messages.Count; i++)
      {
        var m = messages[i];
        var user = userStore.GetById(m.SenderUsername)
                   ?? throw new InvalidOperationException($"Message with ID {m.Id} references a missing user");

        result.Add(new MessageDTO
        {
          Id = m.Id,
          Sender = user.Username,
          Content = m.Content,
          Timestamp = m.Timestamp
        });
      }

      return result;
    });
  }
  #endregion

  #region GET MESSAGES AFTER ID
  /// <summary>
  /// Returns all messages with Id greater than the specified lastMessageId.
  /// </summary>
  /// <param name="lastMessageId">The last message ID that the client has seen.</param>
  /// <returns>
  /// A read-only list of MessageDTO representing all messages newer than lastMessageId.
  /// </returns>
  public IReadOnlyList<MessageDTO> GetMessagesAfter(int lastMessageId)
  {
    return WithRead(() =>
    {
      var result = new List<MessageDTO>();

      // Messages are stored in chronological order, so we can scan forward.
      foreach (var m in messages)
      {
        if (m.Id > lastMessageId)
        {
          var user = userStore.GetById(m.SenderUsername)
                     ?? throw new InvalidOperationException(
                        $"Message with ID {m.Id} references a missing user");

          result.Add(new MessageDTO
          {
            Id = m.Id,
            Sender = user.Username,
            Content = m.Content,
            Timestamp = m.Timestamp // timestamp automatically set when message was created
          });
        }
      }
      return result;
    });
  }
  #endregion

  #region REMOVE MESSAGE BY ID
  /// <summary>
  /// Removes a chat message with the specified identifier from the store.
  /// </summary>
  /// <param name="messageId">The unique identifier of the message to remove.</param>
  /// <returns>
  /// True if the message was found and removed.
  /// False if no message with the given <paramref name="messageId"/> exists.
  /// </returns>
  public bool RemoveById(int messageId)
  {
    return WithWrite(() =>
 {
   if (messagesById.TryGetValue(messageId, out var message))
   {
     messages.Remove(message);
     messagesById.Remove(messageId);
     return true;
   }

   ServerLog.Warning($"MessageStore.RemoveById failed: message {messageId} not found");
   return false;
 });
  }
  #endregion

  #region CLEAR ALL MESSAGES
  /// <summary>
  /// Clears all stored messages from both the message list and ID dictionary.
  /// </summary>
  /// <returns>
  /// <c>true</c> if both collections were successfully cleared;
  /// <c>false</c> if either collection is uninitialized (<c>null</c>).
  /// </returns>
  public bool ClearAll()
  {
    return WithWrite(() =>
    {
      try
      {
        messages.Clear();
        messagesById.Clear();
        return true;
      }
      catch (Exception ex)
      {
        ServerLog.Error($"MessageStore.ClearAll failed: {ex}");
        return false;
      }
    });
  }
  #endregion
}
