using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Services;

/// <summary>
/// 工作台日志服务，使用异步写入队列。
/// </summary>
internal sealed class WorkbenchLogger : IDisposable
{
    private const int RetentionDays = 14;
    private readonly Channel<string> queue = Channel.CreateUnbounded<string>();
    private readonly Task writerTask;
    private readonly string logDirectory;
    private bool disposed;

    public WorkbenchLogger(string logDirectory)
    {
        this.logDirectory = logDirectory;
        Directory.CreateDirectory(logDirectory);
        CleanupOldLogs();

        writerTask = Task.Run(WriteLoopAsync);
        Write(LogLevel.Info, "Workbench session started.");
        Write(LogLevel.Info, $"Log directory: {logDirectory}");
    }

    public string CurrentLogPath => Path.Combine(logDirectory, $"workbench-{DateTime.Now:yyyyMMdd}.log");

    public void Info(string message) => Write(LogLevel.Info, message);

    public void Warning(string message) => Write(LogLevel.Warning, message);

    public void Error(string message, Exception? exception = null) => Write(LogLevel.Error, message, exception);

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Write(LogLevel.Info, "Workbench session ended.");
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
                // There is no safe secondary sink in the demo. Drop the entry silently.
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
        foreach (var file in Directory.EnumerateFiles(logDirectory, "workbench-*.log").Select(path => new FileInfo(path)))
        {
            if (file.LastWriteTime < cutoff)
                TryDelete(file.FullName);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort retention cleanup.
        }
    }

    private enum LogLevel
    {
        Info,
        Warning,
        Error
    }
}
