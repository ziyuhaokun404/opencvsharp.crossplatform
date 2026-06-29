using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

public sealed record TemplateMatchMethodViewModel(string Name, TemplateMatchModes Mode, bool HigherIsBetter)
{
    public override string ToString() => Name;
}
