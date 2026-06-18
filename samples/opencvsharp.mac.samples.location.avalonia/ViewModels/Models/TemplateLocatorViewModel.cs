using OpenCvSharp;
using OpenCvSharp.Mac.Core;

namespace OpenCvSharp.Mac.Samples.Location.Avalonia.ViewModels.Models;

public sealed record TemplateLocatorViewModel(string Name, ITemplateLocator Locator, bool UsesTemplateMatchMethod)
{
    public override string ToString() => Name;
}
