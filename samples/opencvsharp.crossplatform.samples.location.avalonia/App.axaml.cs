using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenCvSharp.CrossPlatform.Samples.Location.Avalonia.Views;
using AvaloniaApplication = Avalonia.Application;

namespace OpenCvSharp.CrossPlatform.Samples.Location.Avalonia;

public partial class App : AvaloniaApplication
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();

        base.OnFrameworkInitializationCompleted();
    }
}
