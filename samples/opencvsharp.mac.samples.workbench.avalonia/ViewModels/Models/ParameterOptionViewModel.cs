using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.ViewModels.Models;

public sealed class ParameterOptionViewModel : ObservableObject
{
    private readonly Action<int> selectAction;
    private bool isSelected;

    public ParameterOptionViewModel(int value, string label, Action<int> selectAction)
    {
        Value = value;
        Label = label;
        this.selectAction = selectAction;
        SelectCommand = new RelayCommand(() => this.selectAction(Value));
    }

    public int Value { get; }

    public string Label { get; }

    public ICommand SelectCommand { get; }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}
