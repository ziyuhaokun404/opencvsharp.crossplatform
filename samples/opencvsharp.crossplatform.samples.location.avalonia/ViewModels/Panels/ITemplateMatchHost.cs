using OpenCvSharp.CrossPlatform.Core;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Panels;

public interface ITemplateMatchHost
{
    bool HasImagePair { get; }

    bool IsBusy { get; set; }

    (byte[] Source, byte[] Template) CloneImageBytes();

    ITemplateLocator SelectedLocator { get; }

    ITemplateLocator CreateMatchLocator();

    TemplateLocatorOptions CreateMatchOptions();

    void SetStatus(string text);
}
