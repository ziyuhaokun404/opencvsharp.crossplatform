using System;
using System.Globalization;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace OpenCvSharp.CrossPlatform.Samples.Shared.Logging;

/// <summary>
/// Async file logger with non-blocking write queue, daily log rotation, and retention cleanup.
/// </summary>
public abstract class AsyncFileLogger : IDisposable
{
    private const int RetentionDays = 14;

    private readonly Channel<string> queue = Channel.CreateUnbounded<string>();
    private readonly Task writerTask;
    private readonly string logDirectory;
    private readonly string logFilePrefix;
    private readonly string retentionGlobPattern;
    private bool disposed;

    protected AsyncFileLogger(string logDirectory, string logFilePrefix, string retentionGlobPattern)
    {
        this.logDirectory = logDirectory;
        this.logFilePrefix = logFilePrefix;
        this.retentionGlobPattern = retentionGlobPattern;
        Directory.CreateDirectory(logDirectory);
        CleanupOldLogs();
        writerTask = Task.Run(WriteLoopAsync);
    }

    protected string CurrentLogPath =>
        Path.Combine(logDirectory, $"{logFilePrefix}-{DateTime.Now:yyyyMMdd}.log");

    public void Info(string message) => Write(LogLevel.Info, message);

    public void Warning(string message) => Write(LogLevel.Warning, message);

    public void Error(string message, Exception? exception = null) =>
        Write(LogLevel.Error, message, exception);

    protected void WritePerf(string message) => Write(LogLevel.Perf, message);

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        OnSessionEnding();
        queue.Writer.TryComplete();

        try
        {
            writerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Logging must never block app shutdown.
        }
    }

    protected virtual void OnSessionEnding()
    {
    }

    private void Write(LogLevel level, string message, Exception? exception = null)
    {
        if (disposed && level != LogLevel.Info)
            return;

        var line = FormatLine(level, message, exception);
        queue.Writer.TryWrite(line);
    }

    private async Task WriteLoopAsync()
    {
        await foreach (var line in queue.Reader.ReadAllAsync())
        {
            try
            {
                await File.AppendAllTextAsync(CurrentLogPath, line + Environment.NewLine);
            }
            catch
            {
                // Drop silently when no secondary sink is available.
            }
        }
    }

    private static string FormatLine(LogLevel level, string message, Exception? exception)
    {
        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
        var line = $"[{timestamp}] [{level}] {message}";
        return exception is null
            ? line
            : $"{line}{Environment.NewLine}{exception}";
    }

    private void CleanupOldLogs()
    {
        var cutoff = DateTime.Now.AddDays(-RetentionDays);
        foreach (var file in Directory.EnumerateFiles(logDirectory, retentionGlobPattern))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTime < cutoff)
            {
                try { File.Delete(file); } catch { /* best case */ }
            }
        }
    }

    private enum LogLevel
    {
        Info,
        Warning,
        Error,
        Perf
    }
}
