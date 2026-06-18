using System;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.ViewModels.Models;

/// <summary>
/// 处理流程步骤数据模型。
/// </summary>
public sealed record PipelineStepModel(
    Guid Id,
    string Number,
    string Name,
    string Parameters,
    bool Enabled,
    int ParamA,
    int ParamB,
    bool Clamp);
