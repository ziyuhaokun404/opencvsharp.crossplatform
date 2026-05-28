using System;
using System.Globalization;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using OpenCvSharp.TemplateMatching;

namespace OpenCvSharp.Demo.TemplateMatch.Avalonia;

/// <summary>
/// Async file logger with non-blocking write queue, identical to WorkbenchLogger pattern.
/// Writes daily log files with automatic retention cleanup.
/// </summary>
internal sealed class PerformanceLogger : IDisposable
{
    private const int RetentionDays = 14;
    private readonly Channel<string> queue = Channel.CreateUnbounded<string>();
    private readonly Task writerTask;
    private readonly string logDirectory;
    private bool disposed;

    public PerformanceLogger()
    {
        logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenCvSharp.Demo.TemplateMatch.Avalonia",
            "logs");
        Directory.CreateDirectory(logDirectory);
        CleanupOldLogs();
        writerTask = Task.Run(WriteLoopAsync);
        Log(LogLevel.Info, "Session started.");
    }

    public string CurrentLogPath => Path.Combine(logDirectory, $"template-match-{DateTime.Now:yyyyMMdd}.log");

    public void LogProfile(ProfileResult profile)
    {
        Log(LogLevel.Perf, profile.ToDisplayText().TrimEnd());
    }

    public void Info(string message) => Log(LogLevel.Info, message);

    public void Warning(string message) => Log(LogLevel.Warning, message);

    public void Error(string message, Exception? exception = null)
    {
        var text = exception is null ? message : $"{message}\n{exception}";
        Log(LogLevel.Error, text);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        Log(LogLevel.Info, "Session ended.");
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

    private void Log(LogLevel level, string message)
    {
        if (disposed && level != LogLevel.Info)
            return;

        var timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
        var line = $"[{timestamp}] [{level}] {message}";
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
                // Drop silently.
            }
        }
    }

    private void CleanupOldLogs()
    {
        var cutoff = DateTime.Now.AddDays(-RetentionDays);
        foreach (var file in Directory.EnumerateFiles(logDirectory, "template-match-*.log"))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTime < cutoff)
            {
                try { File.Delete(file); } catch { /* best-effort */ }
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
