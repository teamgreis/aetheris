using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Aetheris.Studio.Core;
using Aetheris.Studio.Core.ViewModels;

namespace Aetheris.Studio;

public partial class App : Application
{
    private IHost? _host;
    public static IServiceProvider? Services { get; private set; }
    public static Window? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                var storagePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Aetheris", "Profiles"
                );

                services.AddSingleton<IProfileRepository>(sp =>
                    new FileProfileRepository(storagePath, sp.GetRequiredService<ILogger<FileProfileRepository>>()));

                services.AddSingleton<IQrCodeService, QrCodeService>();
                services.AddSingleton<ITransferService, TransferService>();

                // UI Services (implemented in UI project)
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<IFilePickerService, FilePickerService>();

                services.AddTransient<AppViewModel>();
                services.AddTransient<ProfileListViewModel>();
                services.AddTransient<ProfileDetailViewModel>();

                services.AddLogging(builder =>
                {
                    builder.AddDebug();
                    builder.AddConsole();
                });
            })
            .Build();

        _host.Start();
        Services = _host.Services;

        var mainWindow = new MainWindow
        {
            ViewModel = _host.Services.GetRequiredService<AppViewModel>()
        };
        MainWindow = mainWindow;

        // WinUI 3 has no Application.OnExit override: dispose the host when the window closes
        mainWindow.Closed += (_, _) => _host?.Dispose();

        mainWindow.Activate();

        base.OnLaunched(args);
    }
}
