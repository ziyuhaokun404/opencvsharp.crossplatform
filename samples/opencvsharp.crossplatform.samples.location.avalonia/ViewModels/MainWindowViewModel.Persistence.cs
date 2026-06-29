using System;
using System.IO;
using System.Text.Json;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Application.Imaging;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels.Models;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.ViewModels;

public sealed partial class MainWindowViewModel
{
    private bool LoadPersistedImages()
    {
        try
        {
            var settings = settingsStore.Load();
            if (settings is null)
                return false;

            if (settings.SourceImageBytes.Length == 0 || settings.TemplateImageBytes.Length == 0)
                return false;

            imageSession.Load(settings.SourceImageBytes, settings.TemplateImageBytes);
            if (settings.ContourSettings is not null)
                Contour.ApplySettings(settings.ContourSettings);
            else
                Contour.ApplySettings(Contour.Locator.CurrentSettings);

            ReplaceSourceImage(AvaloniaImagePreview.FromBytes(settings.SourceImageBytes));
            RefreshTemplatePreview();
            UpdateSourceSizeDisplay();
            StatusText = "已加载上次保存的源图和模板图。";
            ClearMatchResult();
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException or NotSupportedException)
        {
            StatusText = "无法加载已保存的图像配置，已忽略。";
            return false;
        }
    }

    private void SaveCurrentImages()
    {
        if (!imageSession.HasPair)
            return;

        settingsStore.Save(new TemplateMatchSettings(
            imageSession.SourceBytes.ToArray(),
            imageSession.TemplateBytes.ToArray(),
            Contour.Locator.CurrentSettings));
    }
}
