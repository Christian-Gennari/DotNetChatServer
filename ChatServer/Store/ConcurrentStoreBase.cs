using System.Runtime.CompilerServices;
using ChatServer.Logger;

namespace ChatServer.Store;

public abstract class ConcurrentStoreBase
{
  // Lock that coordinates safe access to shared state.
  // Readers can proceed together while writers get exclusive access.
  protected readonly ReaderWriterLockSlim locker = new(LockRecursionPolicy.NoRecursion);

  // Runs a read-only operation while protecting the shared collections
  // from concurrent writes. Many reads can run at the same time.
  protected T WithRead<T>(Func<T> action, [CallerMemberName] string caller = "")
  {
    locker.EnterReadLock();
    try
    {
      return action();
    }
    catch (Exception ex)
    {
      ServerLog.Error($"READ failure in {GetType().Name}.{caller}: {ex}");
      throw;
    }
    finally
    {
      locker.ExitReadLock();
    }
  }

  // Runs a write operation that modifies shared state.
  // Only one writer is allowed at a time and readers are paused.
  protected T WithWrite<T>(Func<T> action, [CallerMemberName] string caller = "")
  {
    locker.EnterWriteLock();
    try
    {
      return action();
    }
    catch (Exception ex)
    {
      ServerLog.Error($"WRITE failure in {GetType().Name}.{caller}: {ex}");
      throw;
    }
    finally
    {
      locker.ExitWriteLock();
    }
  }

  // Overload for write operations that do not return a value.
  protected void WithWrite(Action action, [CallerMemberName] string caller = "")
  {
    locker.EnterWriteLock();
    try
    {
      action();
    }
    catch (Exception ex)
    {
      ServerLog.Error($"WRITE failure in {GetType().Name}.{caller}: {ex}");
      throw;
    }
    finally
    {
      locker.ExitWriteLock();
    }
  }
}
