using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenCvSharp.CrossPlatform.Samples.Workbench.Avalonia.ViewModels.Models;

/// <summary>
/// 处理流程节点视图模型，用于在流程面板中显示节点。
/// </summary>
public sealed class PipelineNodeViewModel : ObservableObject
{
    private PipelineNodeViewModel(
        int index,
        int stepIndex,
        bool isStep,
        string nodeKind,
        string title,
        string subtitle,
        bool enabled,
        bool hasError,
        bool isCompact,
        bool hasNext,
        Action<int>? toggleAction)
    {
        Index = index;
        StepIndex = stepIndex;
        IsStep = isStep;
        NodeKind = nodeKind;
        Title = title;
        Subtitle = subtitle;
        Enabled = enabled;
        HasError = hasError;
        HasNext = hasNext;
        IsCompact = isCompact;
        var stateColor = StateColor(nodeKind, enabled, hasError, subtitle);
        AccentBrush = new SolidColorBrush(stateColor);
        StatusSurfaceBrush = new SolidColorBrush(Color.FromArgb(34, stateColor.R, stateColor.G, stateColor.B));
        StatusBorderBrush = new SolidColorBrush(Color.FromArgb(96, stateColor.R, stateColor.G, stateColor.B));
        StatusTextBrush = new SolidColorBrush(stateColor);
        StatusText = hasError ? "错误" : isStep ? enabled ? "已启用" : "已禁用" : nodeKind == "Input" ? "输入" : subtitle;
        ToggleText = enabled ? "开" : "关";
        ToggleCommand = new RelayCommand(() => toggleAction?.Invoke(stepIndex));
    }

    private bool isCurrent;

    public int Index { get; }

    public int StepIndex { get; }

    public bool IsStep { get; }

    public string NodeKind { get; }

    public string Title { get; }

    public string Subtitle { get; }

    public bool Enabled { get; }

    public bool HasError { get; }

    public bool HasNext { get; }

    public bool IsCompact { get; }

    public IBrush AccentBrush { get; }

    public IBrush StatusSurfaceBrush { get; }

    public IBrush StatusBorderBrush { get; }

    public IBrush StatusTextBrush { get; }

    public string StatusText { get; }

    public string ToggleText { get; }

    public int SwitchKnobColumn => Enabled ? 1 : 0;

    public ICommand ToggleCommand { get; }

    public bool IsCurrent
    {
        get => isCurrent;
        set
        {
            if (!SetProperty(ref isCurrent, value))
                return;

            OnPropertyChanged(nameof(CardBorderBrush));
            OnPropertyChanged(nameof(CardBorderThickness));
        }
    }

    public IBrush CardBorderBrush => IsCurrent ? new SolidColorBrush(Color.Parse("#2563EB")) : StatusBorderBrush;

    public Thickness CardBorderThickness => IsCurrent ? new Thickness(2) : new Thickness(1);

    public bool IsSubtitleVisible => !IsCompact;

    public bool IsToggleVisible => IsStep;

    public double CardHeight => IsCompact ? 58 : 104;

    public Thickness CardPadding => IsCompact ? new Thickness(10, 7) : new Thickness(10);

    public static PipelineNodeViewModel CreateInput(int index, string subtitle, bool hasNext, bool isCompact)
    {
        return new PipelineNodeViewModel(index, -1, false, "Input", "输入", subtitle, true, false, isCompact, hasNext, null);
    }

    public static PipelineNodeViewModel CreateResult(int index, string subtitle, bool hasNext, bool isCompact)
    {
        return new PipelineNodeViewModel(index, -1, false, "Result", "结果", subtitle, subtitle == "就绪", subtitle == "处理流程错误", isCompact, hasNext, null);
    }

    public static PipelineNodeViewModel CreateStep(int index, int stepIndex, PipelineStepModel step, bool hasNext, bool hasError, bool isCompact, Action<int> toggleAction)
    {
        return new PipelineNodeViewModel(index, stepIndex, true, hasError ? "Error" : step.Enabled ? "Enabled" : "Disabled", $"{step.Number}  {step.Name}", step.Parameters, step.Enabled, hasError, isCompact, hasNext, toggleAction);
    }

    private static Color StateColor(string nodeKind, bool enabled, bool hasError, string subtitle)
    {
        if (hasError)
            return Color.Parse("#DC2626");

        return nodeKind switch
        {
            "Input" => Color.Parse("#2563EB"),
            "Enabled" => Color.Parse("#16A34A"),
            "Disabled" => Color.Parse("#94A3B8"),
            "Result" when subtitle == "就绪" => Color.Parse("#16A34A"),
            _ => enabled ? Color.Parse("#16A34A") : Color.Parse("#94A3B8")
        };
    }
}
