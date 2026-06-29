using System.Collections.Generic;
using System.Linq;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Operators.BuiltIn;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Operators;

/// <summary>
/// 算子注册表，管理所有可用算子。
/// </summary>
public sealed class OperatorRegistry
{
    private readonly List<IImageOperator> operators = [];

    public OperatorRegistry()
    {
        // 注册内置算子
        Register(new GrayscaleOperator());
        Register(new GaussianBlurOperator());
        Register(new CannyEdgeOperator());
        Register(new BinaryThresholdOperator());
        Register(new AdaptiveThresholdOperator());
        Register(new MorphologyCloseOperator());
        Register(new FindContoursOperator());
        Register(new SharpenOperator());
        Register(new ResizeOperator());
        Register(new RotateOperator());
    }

    public void Register(IImageOperator op) => operators.Add(op);

    public IReadOnlyList<IImageOperator> GetAll() => operators;

    public IImageOperator? FindByName(string name) =>
        operators.FirstOrDefault(op => op.Name == name);

    public IImageOperator? FindById(string id) =>
        operators.FirstOrDefault(op => op.Descriptor.Id == id);
}
