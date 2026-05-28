using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace OpenCvSharp.Demo.Shared;

internal static class OpenCvSharpNativeRuntime
{
    private static bool isRegistered;

    public static void Register()
    {
        if (isRegistered)
            return;

        NativeLibrary.SetDllImportResolver(typeof(Cv2).Assembly, ResolveOpenCvSharpExtern);
        isRegistered = true;
    }

    private static IntPtr ResolveOpenCvSharpExtern(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (libraryName != "OpenCvSharpExtern")
            return IntPtr.Zero;

        var explicitPath = Environment.GetEnvironmentVariable("OPENCVSHARP_EXTERN_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            return NativeLibrary.Load(explicitPath, assembly, searchPath);

        var localPath = Path.Combine(AppContext.BaseDirectory, GetPlatformLibraryFileName());
        return NativeLibrary.Load(localPath, assembly, searchPath);
    }

    private static string GetPlatformLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
            return "OpenCvSharpExtern.dll";

        if (OperatingSystem.IsMacOS())
            return "libOpenCvSharpExtern.dylib";

        if (OperatingSystem.IsLinux())
            return "libOpenCvSharpExtern.so";

        throw new PlatformNotSupportedException("Unsupported platform for OpenCvSharp native runtime.");
    }
}
