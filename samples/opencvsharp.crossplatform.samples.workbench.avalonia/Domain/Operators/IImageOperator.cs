using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;

/// <summary>
/// 图像处理算子接口。
/// </summary>
public interface IImageOperator
{
    /// <summary>算子稳定描述信息。</summary>
    ImageOperatorDescriptor Descriptor { get; }

    /// <summary>算子显示名称</summary>
    string Name => Descriptor.Name;

    /// <summary>算子分类</summary>
    string Category => Descriptor.Category;

    /// <summary>算子描述</summary>
    string Description => Descriptor.Description;

    /// <summary>参数 A 的显示名称（空表示无参数 A）</summary>
    string ParamAName => Descriptor.PrimaryParameter?.DisplayName ?? "";

    /// <summary>参数 B 的显示名称（空表示无参数 B）</summary>
    string ParamBName => Descriptor.SecondaryParameter?.DisplayName ?? "";

    /// <summary>参数 A 的默认值</summary>
    int DefaultParamA => Descriptor.PrimaryParameter?.DefaultValue ?? 0;

    /// <summary>参数 B 的默认值</summary>
    int DefaultParamB => Descriptor.SecondaryParameter?.DefaultValue ?? 0;

    /// <summary>参数 A 的最小值</summary>
    int ParamAMinimum => Descriptor.PrimaryParameter?.Range.Minimum ?? 0;

    /// <summary>参数 A 的最大值</summary>
    int ParamAMaximum => Descriptor.PrimaryParameter?.Range.Maximum ?? 0;

    /// <summary>参数 B 的最小值</summary>
    int ParamBMinimum => Descriptor.SecondaryParameter?.Range.Minimum ?? 0;

    /// <summary>参数 B 的最大值</summary>
    int ParamBMaximum => Descriptor.SecondaryParameter?.Range.Maximum ?? 0;

    /// <summary>是否需要 Clamp 参数</summary>
    bool SupportsClamp => Descriptor.SupportsClamp;

    /// <summary>
    /// 执行图像处理。
    /// </summary>
    Mat Apply(Mat source, IReadOnlyDictionary<string, int> parameters, OperatorExecutionContext context);

    /// <summary>
    /// 执行图像处理。兼容当前 A/B 参数 UI，后续迁移到参数集合后删除。
    /// </summary>
    Mat Apply(Mat source, int paramA, int paramB, bool clamp)
    {
        var values = new Dictionary<string, int>();
        if (Descriptor.PrimaryParameter is { } primary)
            values[primary.Key] = paramA;
        if (Descriptor.SecondaryParameter is { } secondary)
            values[secondary.Key] = paramB;

        return Apply(source, values, new OperatorExecutionContext(clamp));
    }

    /// <summary>
    /// 格式化参数值用于显示。
    /// </summary>
    string FormatParameterValue(string paramName, int value)
    {
        var parameter = Descriptor.Parameters.FirstOrDefault(item => item.DisplayName == paramName);
        return parameter?.FormatValue(value) ?? value.ToString();
    }
}
