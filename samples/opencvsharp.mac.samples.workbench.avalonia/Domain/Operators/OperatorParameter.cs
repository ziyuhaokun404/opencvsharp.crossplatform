using System.Collections.Generic;
using System.Linq;
using OpenCvSharp.Mac.Samples.Workbench.Avalonia.Domain.Shared;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.Domain.Operators;

public sealed record OperatorParameter(
    string Key,
    string DisplayName,
    int DefaultValue,
    ValueRange Range,
    IReadOnlyList<OperatorParameterOption> Options,
    string? Unit = null)
{
    public static OperatorParameter Numeric(
        string key,
        string displayName,
        int defaultValue,
        int minimum,
        int maximum,
        string? unit = null)
    {
        return new OperatorParameter(key, displayName, defaultValue, new ValueRange(minimum, maximum), [], unit);
    }

    public static OperatorParameter Choice(
        string key,
        string displayName,
        int defaultValue,
        IReadOnlyList<OperatorParameterOption> options)
    {
        var minimum = options.Count == 0 ? defaultValue : options.Min(option => option.Value);
        var maximum = options.Count == 0 ? defaultValue : options.Max(option => option.Value);
        return new OperatorParameter(key, displayName, defaultValue, new ValueRange(minimum, maximum), options);
    }

    public string FormatValue(int value)
    {
        var option = Options.FirstOrDefault(item => item.Value == value);
        return option is not null ? option.Label : value.ToString();
    }
}
