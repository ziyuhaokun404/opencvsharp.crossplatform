using System;
using OpenCvSharp;
using CvRect = OpenCvSharp.Rect;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Imaging;

public sealed record RotatedRoiCropResult(
    byte[] TemplateBytes,
    int X,
    int Y,
    int Width,
    int Height,
    double Angle);

public sealed class TemplateImageSession : IDisposable
{
    private byte[] sourceBytes = [];
    private byte[] templateBytes = [];
    private Mat? cachedSourceMat;
    private Mat? cachedTemplateMat;

    public bool HasSource => sourceBytes.Length > 0;

    public bool HasTemplate => templateBytes.Length > 0;

    public bool HasPair => HasSource && HasTemplate;

    public ReadOnlyMemory<byte> SourceBytes => sourceBytes;

    public ReadOnlyMemory<byte> TemplateBytes => templateBytes;

    public int SourceWidth { get; private set; }

    public int SourceHeight { get; private set; }

    public void SetSource(byte[] bytes)
    {
        sourceBytes = bytes;
        InvalidateSourceCache();
        var (width, height) = OpenCvImageCodec.ReadSize(bytes);
        SourceWidth = width;
        SourceHeight = height;
    }

    public void SetTemplate(byte[] bytes)
    {
        templateBytes = bytes;
        InvalidateTemplateCache();
    }

    public void Load(byte[] source, byte[] template)
    {
        sourceBytes = source;
        templateBytes = template;
        InvalidateImageCache();
        var (width, height) = OpenCvImageCodec.ReadSize(sourceBytes);
        SourceWidth = width;
        SourceHeight = height;
    }

    public (byte[] Source, byte[] Template) CloneImageBytes()
        => ((byte[])sourceBytes.Clone(), (byte[])templateBytes.Clone());

    public RotatedRoiCropResult? TryCropTemplateFromRotatedRoi(RotatedRect roi)
    {
        if (!HasSource || roi.Size.Width < 4 || roi.Size.Height < 4)
            return null;

        using var source = OpenCvImageCodec.Decode(sourceBytes);
        using var rotation = Cv2.GetRotationMatrix2D(roi.Center, -roi.Angle, 1);
        using var rotated = new Mat();
        Cv2.WarpAffine(source, rotated, rotation, source.Size(), InterpolationFlags.Linear, BorderTypes.Replicate);

        var x = (int)Math.Round(roi.Center.X - roi.Size.Width / 2);
        var y = (int)Math.Round(roi.Center.Y - roi.Size.Height / 2);
        var width = (int)Math.Round(roi.Size.Width);
        var height = (int)Math.Round(roi.Size.Height);
        var clamped = new Rect(x, y, width, height).Intersect(new Rect(0, 0, rotated.Width, rotated.Height));
        if (clamped.Width < 4 || clamped.Height < 4)
            return null;

        using var cropped = new Mat(rotated, new CvRect(clamped.X, clamped.Y, clamped.Width, clamped.Height));
        return new RotatedRoiCropResult(
            OpenCvImageCodec.EncodePng(cropped),
            clamped.X,
            clamped.Y,
            clamped.Width,
            clamped.Height,
            roi.Angle);
    }

    public Mat GetSourceMat()
    {
        cachedSourceMat ??= OpenCvImageCodec.Decode(sourceBytes);
        return cachedSourceMat;
    }

    public Mat GetTemplateMat()
    {
        cachedTemplateMat ??= OpenCvImageCodec.Decode(templateBytes);
        return cachedTemplateMat;
    }

    public void Dispose()
    {
        InvalidateImageCache();
        sourceBytes = [];
        templateBytes = [];
        SourceWidth = 0;
        SourceHeight = 0;
    }

    private void InvalidateSourceCache()
    {
        cachedSourceMat?.Dispose();
        cachedSourceMat = null;
    }

    private void InvalidateTemplateCache()
    {
        cachedTemplateMat?.Dispose();
        cachedTemplateMat = null;
    }

    private void InvalidateImageCache()
    {
        InvalidateSourceCache();
        InvalidateTemplateCache();
    }
}
