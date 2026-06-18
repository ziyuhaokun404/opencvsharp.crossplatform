using System;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.Application.Pipeline;

public sealed record PipelineStepResult(
    Guid StepId,
    string OperatorName,
    bool Skipped,
    TimeSpan Elapsed,
    string? ErrorMessage);
