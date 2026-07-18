using HueBar.Core;

namespace HueBar;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Opens the connect pane directly (handy for reconnecting, and for verifying the
        // dialog layout in isolation): HueBar.exe --settings
        if (args.Contains("--settings", StringComparer.OrdinalIgnoreCase))
        {
            Application.Run(new SettingsForm(new HueClient(), AppSettings.Load()));
            return;
        }

        Application.Run(new TrayApplicationContext());
    }
}
