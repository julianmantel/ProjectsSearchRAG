using System;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProjectsSearchRAG.App.ViewModels;
using ProjectsSearchRAG.Data.Context;
using ProjectsSearchRAG.Infrastructure;

namespace ProjectsSearchRAG.App;

public partial class App : Application
{
    private readonly IHost _host;
    private IServiceScope? _appScope;

    public App()
    {
        LoadEnvFile(AppDomain.CurrentDomain.BaseDirectory);

        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(AppDomain.CurrentDomain.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
                config.AddUserSecrets<App>(optional: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddInfrastructure(context.Configuration);
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();

        await ApplyMigrationsAsync();

        _appScope = _host.Services.CreateScope();
        var mainWindow = _appScope.ServiceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void LoadEnvFile(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        while (dir != null)
        {
            var envPath = Path.Combine(dir.FullName, ".env");
            if (File.Exists(envPath))
            {
                DotNetEnv.Env.Load(envPath);
                return;
            }
            dir = dir.Parent;
        }
    }

    private async Task ApplyMigrationsAsync()
    {
        using var scope = _host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            await dbContext.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"No se pudo aplicar la migración de la base de datos.\n\n{ex.Message}",
                "ProjectsSearchRAG",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _appScope?.Dispose();
        using (_host)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(5));
        }
        base.OnExit(e);
    }
}
