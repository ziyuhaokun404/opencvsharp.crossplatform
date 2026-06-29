using System;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Application.Pipeline;

public sealed record PipelineStepResult(
    Guid StepId,
    string OperatorName,
    bool Skipped,
    TimeSpan Elapsed,
    string? ErrorMessage);
