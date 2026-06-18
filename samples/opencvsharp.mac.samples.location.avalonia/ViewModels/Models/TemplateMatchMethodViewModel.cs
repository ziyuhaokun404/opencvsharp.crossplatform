using OpenCvSharp;
using OpenCvSharp.Mac.Core;

namespace OpenCvSharp.Mac.Samples.Location.Avalonia.ViewModels.Models;

public sealed record TemplateMatchMethodViewModel(string Name, TemplateMatchModes Mode, bool HigherIsBetter)
{
    public override string ToString() => Name;
}
