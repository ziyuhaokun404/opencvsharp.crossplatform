using System.Collections.Generic;
using System.Linq;
using OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators.BuiltIn;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.Domain.Operators;

/// <summary>
/// 算子注册表，管理所有可用算子。
/// </summary>
public sealed class OperatorRegistry
{
    private readonly List<IImageOperator> operators = [];

    public OperatorRegistry()
    {
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
