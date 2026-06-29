using System;
using Avalonia.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Core;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Imaging;

internal static class AvaloniaImagePreview
{
    public static Bitmap? FromBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
            return null;

        return OpenCvImageCodec.CreateBitmap(bytes);
    }

    public static Bitmap? CreateTemplatePreview(byte[] templateBytes, ITemplateLocator locator)
    {
        if (templateBytes.Length == 0)
            return null;

        if (locator is not ContourTemplateLocator contourLocator)
            return FromBytes(templateBytes);

        using var template = OpenCvImageCodec.Decode(templateBytes);
        using var preview = contourLocator.CreateTemplateContourPreview(template);
        return OpenCvImageCodec.CreateBitmap(OpenCvImageCodec.EncodePng(preview));
    }
}
