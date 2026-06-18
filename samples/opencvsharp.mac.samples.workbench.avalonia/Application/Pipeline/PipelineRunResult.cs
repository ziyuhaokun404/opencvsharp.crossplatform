using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.Application.Pipeline;

public sealed class PipelineRunResult : IDisposable
{
    public PipelineRunResult(
        Mat? output,
        IReadOnlyList<PipelineStepResult> steps,
        Guid? failedStepId,
        int failedStepIndex,
        Exception? exception)
    {
        Output = output;
        Steps = steps;
        FailedStepId = failedStepId;
        FailedStepIndex = failedStepIndex;
        Exception = exception;
    }

    public Mat? Output { get; }

    public IReadOnlyList<PipelineStepResult> Steps { get; }

    public Guid? FailedStepId { get; }

    public int FailedStepIndex { get; }

    public Exception? Exception { get; }

    public bool Succeeded => Exception is null;

    public void Dispose()
    {
        Output?.Dispose();
    }
}
