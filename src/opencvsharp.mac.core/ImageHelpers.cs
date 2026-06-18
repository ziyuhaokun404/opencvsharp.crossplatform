using OpenCvSharp;

namespace OpenCvSharp.Mac.Core;

internal static class ImageHelpers
{
    /// <summary>
    /// 将图像转换为灰度图。调用方负责释放返回的 Mat。
    /// </summary>
    public static Mat ConvertToGray(Mat source)
    {
        var gray = new Mat();
        Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);
        return gray;
    }
}
