using System.Runtime.CompilerServices;
using OpenCvSharp.CrossPlatform.Samples.Shared;

namespace OpenCvSharp.CrossPlatform.Core.Tests;

internal static class OpenCvSharpTestBootstrap
{
    [ModuleInitializer]
    internal static void Initialize() => OpenCvSharpNativeRuntime.Register();
}
