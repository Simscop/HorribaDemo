using System.Configuration;
using System.Data;
using System.Windows;
using Serilog;

namespace HorribaDemo;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    public App()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            .CreateLogger();
    }
}

