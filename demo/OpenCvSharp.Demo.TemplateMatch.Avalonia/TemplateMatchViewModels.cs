using OpenCvSharp;
using OpenCvSharp.Core;

namespace OpenCvSharp.Demo.TemplateMatch.Avalonia;

public sealed record TemplateLocatorViewModel(string Name, ITemplateLocator Locator, bool UsesTemplateMatchMethod)
{
    public override string ToString() => Name;
}

public sealed record TemplateMatchMethodViewModel(string Name, TemplateMatchModes Mode, bool HigherIsBetter)
{
    public override string ToString() => Name;
}

public sealed record MatchOverlayViewModel(int X, int Y, int Width, int Height, bool IsBestMatch);
