using System;
using OpenCvSharp.CrossPlatform.Core;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Matching;

internal static class TemplateLocatorSnapshotFactory
{
    public static ITemplateLocator CreateSnapshot(ITemplateLocator locator)
    {
        if (locator is not ContourTemplateLocator contour)
            return locator;

        var snapshot = new ContourTemplateLocator();
        snapshot.ApplySettings(contour.CurrentSettings);
        return snapshot;
    }

    public static void DisposeOwnedLocator(ITemplateLocator locator, ITemplateLocator activeLocator)
    {
        if (ReferenceEquals(locator, activeLocator))
            return;

        if (locator is IDisposable disposable)
            disposable.Dispose();
    }
}
