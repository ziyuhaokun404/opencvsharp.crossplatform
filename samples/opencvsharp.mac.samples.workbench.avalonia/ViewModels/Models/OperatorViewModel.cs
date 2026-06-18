using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.ViewModels.Models;

/// <summary>
/// 算子视图模型，用于在 UI 中显示算子信息。
/// </summary>
public sealed class OperatorViewModel
{
    public OperatorViewModel(
        string category,
        string name,
        string description,
        string paramA,
        string paramB,
        int defaultA,
        int defaultB,
        Action<OperatorViewModel> addAction)
    {
        Category = category;
        Name = name;
        Description = description;
        ParamA = paramA;
        ParamB = paramB;
        DefaultA = defaultA;
        DefaultB = defaultB;
        CategoryDisplay = category.ToUpperInvariant();
        AddCommand = new RelayCommand(() => addAction(this));
    }

    public string Category { get; }

    public string CategoryDisplay { get; }

    public string Name { get; }

    public string Description { get; }

    public string ParamA { get; }

    public string ParamB { get; }

    public int DefaultA { get; }

    public int DefaultB { get; }

    public ICommand AddCommand { get; }
}
