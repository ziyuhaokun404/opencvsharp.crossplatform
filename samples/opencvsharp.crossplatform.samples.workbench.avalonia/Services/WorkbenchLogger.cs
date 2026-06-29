using System.IO;
using OpenCvSharp.CrossPlatform.Samples.Shared.Logging;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Services;

/// <summary>
/// 工作台日志服务，使用异步写入队列。
/// </summary>
internal sealed class WorkbenchLogger : AsyncFileLogger
{
    public WorkbenchLogger(string logDirectory)
        : base(logDirectory, "workbench", "workbench-*.log")
    {
        Info("Workbench session started.");
        Info($"Log directory: {logDirectory}");
    }

    protected override void OnSessionEnding() => Info("Workbench session ended.");
}
