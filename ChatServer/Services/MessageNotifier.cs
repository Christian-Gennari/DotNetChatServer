using ChatServer.Logger;

namespace ChatServer.Services
{
  /// <summary>
  /// Signals long-polling clients when a new chat message arrives.
  /// Clients await a task returned by <see cref="WaitForNextMessageAsync"/>,
  /// and <see cref="NotifyNewMessage"/> completes all pending waiters.
  /// </summary>
  public class MessageNotifier
  {
    /// <summary>
    /// Pending long-poll waiters, each represented by a TaskCompletionSource.
    /// Protected by <see cref="waitersLock"/>.
    /// </summary>
    private readonly List<TaskCompletionSource<bool>> waiters = [];

    /// <summary>
    /// Synchronizes access to <see cref="waiters"/>.
    /// </summary>
    private readonly Lock waitersLock = new();

    /// <summary>
    /// Returns a task that completes when the next message is published.
    /// Long-poll endpoints await this task instead of looping.
    /// </summary>
    public Task WaitForNextMessageAsync()
    {
      var tcs = new TaskCompletionSource<bool>(
          TaskCreationOptions.RunContinuationsAsynchronously);

      lock (waitersLock)
        waiters.Add(tcs);

      ServerLog.Trace("Registered long-poll waiter");
      return tcs.Task;
    }

    /// <summary>
    /// Wakes all current waiters, signaling that a new message has arrived.
    /// </summary>
    public void NotifyNewMessage()
    {
      List<TaskCompletionSource<bool>> snapshot;

      lock (waitersLock)
      {
        snapshot = new List<TaskCompletionSource<bool>>(waiters);
        waiters.Clear();
      }

      ServerLog.Info($"Notifier: Waking {snapshot.Count} waiting clients");

      foreach (var waiter in snapshot)
        waiter.TrySetResult(true);
    }
  }
}
