using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

public sealed record TemplateLocatorViewModel(string Name, ITemplateLocator Locator, bool UsesTemplateMatchMethod)
{
    public override string ToString() => Name;
}
