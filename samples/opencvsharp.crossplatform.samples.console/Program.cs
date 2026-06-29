using OpenCvSharp;
using OpenCvSharp.CrossPlatform.Samples.Shared;

namespace OpenCvSharp.CrossPlatform.Samples.Console;

internal static class Program
{
    public static void Main()
    {
        OpenCvSharpNativeRuntime.Register();

        var outputDir = Path.Combine(AppContext.BaseDirectory, "output");
        Directory.CreateDirectory(outputDir);

        using var image = new Mat(new Size(640, 360), MatType.CV_8UC3, Scalar.White);

        Cv2.Rectangle(image, new Rect(70, 70, 190, 190), new Scalar(60, 120, 230), -1);
        Cv2.Circle(image, new Point(420, 175), 95, new Scalar(230, 90, 60), -1);
        Cv2.PutText(
            image,
            "OpenCvSharp Cross-Platform",
            new Point(54, 315),
            HersheyFonts.HersheySimplex,
            1.1,
            Scalar.Black,
            2,
            LineTypes.AntiAlias);

        using var gray = new Mat();
        using var edges = new Mat();
        Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.Canny(gray, edges, 80, 160);

        var imagePath = Path.Combine(outputDir, "source.png");
        var edgesPath = Path.Combine(outputDir, "edges.png");
        Cv2.ImWrite(imagePath, image);
        Cv2.ImWrite(edgesPath, edges);

        System.Console.WriteLine($"OpenCV: {Cv2.GetVersionString()}");
        System.Console.WriteLine($"Source image: {imagePath}");
        System.Console.WriteLine($"Canny edges:  {edgesPath}");
    }
}
