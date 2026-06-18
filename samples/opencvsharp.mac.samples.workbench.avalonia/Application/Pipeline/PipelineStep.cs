using System;
using System.Collections.Generic;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.Application.Pipeline;

public sealed record PipelineStep(
    Guid Id,
    string OperatorId,
    string OperatorName,
    IReadOnlyDictionary<string, int> Parameters,
    bool IsEnabled,
    bool ClampOutput)
{
    public int GetParameter(string key, int defaultValue = 0)
    {
        return Parameters.TryGetValue(key, out var value) ? value : defaultValue;
    }
}
