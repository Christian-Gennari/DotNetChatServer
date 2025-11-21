using System.Collections.Concurrent;
using System.Globalization;

namespace ChatServer.Logger
{
  public enum ServerLogLevel
  {
    Trace,
    Info,
    Warning,
    Success,
    Error,
    Critical
  }
  /// <summary>
  /// Global asynchronous server logger with a background write queue, file rotation,
  /// graceful shutdown, severity filtering, and structured log formatting.
  /// Safe to call from anywhere in the server.
  /// </summary>
  public static class ServerLog
  {
    private static readonly BlockingCollection<string> queue = [];
    private static readonly CancellationTokenSource cts = new();

    private static readonly string logDirectory = "logs";
    private static readonly long maxFileSizeBytes = 10 * 1024 * 1024; // 10 MB rotation
    private static readonly ServerLogLevel minimumLevel = ServerLogLevel.Trace;

    private static readonly Task writerTask;
    private static string currentLogFile;

    // Static constructor initializes the logger once for the entire app
    static ServerLog()
    {
      Directory.CreateDirectory(logDirectory);
      currentLogFile = CreateLogFilePath();

      writerTask = Task.Run(() => WriterLoop(cts.Token));
    }

    // ---------------------------------------------------------------
    // Public logging API
    // ---------------------------------------------------------------

    public static void Trace(string message) => Write(ServerLogLevel.Trace, message);
    public static void Info(string message) => Write(ServerLogLevel.Info, message);
    public static void Warning(string message) => Write(ServerLogLevel.Warning, message);
    public static void Success(string message) => Write(ServerLogLevel.Success, message);
    public static void Error(string message) => Write(ServerLogLevel.Error, message);
    public static void Critical(string message) => Write(ServerLogLevel.Critical, message);
    private static void Write(ServerLogLevel level, string message)
    {
      if (level < minimumLevel)
        return;

      string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
      string line = $"[{timestamp}] [{level}] {message}";
      queue.Add(line);
    }

    // ---------------------------------------------------------------
    // Background writer loop
    // ---------------------------------------------------------------

    private static async Task WriterLoop(CancellationToken token)
    {
      try
      {
        foreach (var line in queue.GetConsumingEnumerable(token))
        {
          try
          {
            RotateIfNeeded();
            await File.AppendAllTextAsync(currentLogFile, line + Environment.NewLine, token);
          }
          catch
          {
            Console.WriteLine(line);
          }
        }
      }
      catch (OperationCanceledException)
      {
      }
    }

    // ---------------------------------------------------------------
    // File rotation
    // ---------------------------------------------------------------

    private static void RotateIfNeeded()
    {
      try
      {
        var info = new FileInfo(currentLogFile);

        if (info.Exists && info.Length >= maxFileSizeBytes)
        {
          string rotated = currentLogFile.Replace(".log",
              $".{DateTime.Now:yyyyMMdd_HHmmss}.log");

          File.Move(currentLogFile, rotated);
          currentLogFile = CreateLogFilePath();
        }
      }
      catch
      {
      }
    }

    private static string CreateLogFilePath()
    {
      string date = DateTime.Now.ToString("yyyy-MM-dd");
      return Path.Combine(logDirectory, $"server-{date}.log");
    }

    // ---------------------------------------------------------------
    // Shutdown handling
    // ---------------------------------------------------------------

    public static void Shutdown()
    {
      try
      {
        queue.CompleteAdding();
        cts.CancelAfter(3000);
        writerTask.Wait(3000);
      }
      catch
      {
      }
      finally
      {
        cts.Dispose();
      }
    }
  }
}
