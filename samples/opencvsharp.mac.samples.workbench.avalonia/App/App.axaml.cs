using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OpenCvSharp.Mac.Samples.Workbench.Avalonia.Views;
using AvaloniaApplication = Avalonia.Application;

namespace OpenCvSharp.Mac.Samples.Workbench.Avalonia;

public partial class App : AvaloniaApplication
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
