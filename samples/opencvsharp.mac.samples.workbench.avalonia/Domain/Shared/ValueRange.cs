using System;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia.Domain.Shared;

public readonly record struct ValueRange(int Minimum, int Maximum)
{
    public int Clamp(int value) => Math.Clamp(value, Minimum, Maximum);
}
