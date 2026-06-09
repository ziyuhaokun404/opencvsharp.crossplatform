using System;
using System.IO;
using System.Text.Json;

namespace OpenCvSharp.Demo.TemplateMatch.Avalonia;

public sealed class TemplateMatchSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string settingsPath;

    public TemplateMatchSettingsStore()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OpenCvSharp.Demo.TemplateMatch.Avalonia");
        settingsPath = Path.Combine(settingsDirectory, "settings.json");
    }

    public TemplateMatchSettings? Load()
    {
        if (!File.Exists(settingsPath))
            return null;

        using var stream = File.OpenRead(settingsPath);
        return JsonSerializer.Deserialize<TemplateMatchSettings>(stream)
            ?? throw new InvalidDataException("已保存的图像配置为空。");
    }

    public void Save(TemplateMatchSettings settings)
    {
        var settingsDirectory = Path.GetDirectoryName(settingsPath);
        if (settingsDirectory is null)
            throw new InvalidOperationException("无法解析设置目录。");

        Directory.CreateDirectory(settingsDirectory);
        using var stream = File.Create(settingsPath);
        JsonSerializer.Serialize(stream, settings, JsonOptions);
    }
}
