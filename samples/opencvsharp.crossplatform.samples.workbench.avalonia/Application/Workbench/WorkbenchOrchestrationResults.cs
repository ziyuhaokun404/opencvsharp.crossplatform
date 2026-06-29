using System;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Imaging;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Pipeline;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Workbench;

public sealed record WorkbenchPipelineRunResult(
    bool HasImage,
    bool IsSuccess,
    ImageBuffer? Output,
    int FailedStepIndex,
    Exception? Exception)
{
    public static WorkbenchPipelineRunResult NoImage { get; } = new(false, true, null, -1, null);

    public static WorkbenchPipelineRunResult Succeeded(ImageBuffer output) =>
        new(true, true, output, -1, null);

    public static WorkbenchPipelineRunResult Failed(int failedStepIndex, Exception exception) =>
        new(true, false, null, failedStepIndex, exception);
}

public sealed record WorkbenchPreviewResult(
    bool IsSuccess,
    ImageBuffer? Output,
    Exception? Exception)
{
    public static WorkbenchPreviewResult Succeeded(ImageBuffer output) =>
        new(true, output, null);

    public static WorkbenchPreviewResult Failed(Exception exception) =>
        new(false, null, exception);
}

public sealed record DeleteStepResult(PipelineStep Removed, int Index);

public sealed record MoveStepResult(int FromIndex, int ToIndex);

public sealed record ToggleStepResult(PipelineStep Step, bool Enabled, int Index);
