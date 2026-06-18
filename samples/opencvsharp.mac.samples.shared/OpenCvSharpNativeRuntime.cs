using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using OpenCvSharp;

namespace OpenCvSharp.Mac.Samples.Shared;

public static class OpenCvSharpNativeRuntime
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

        var localPath = Path.Combine(AppContext.BaseDirectory, "libOpenCvSharpExtern.dylib");
        return NativeLibrary.Load(localPath, assembly, searchPath);
    }
}
