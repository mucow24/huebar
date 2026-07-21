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

        // Two tray instances would mean two identical icons and two last-write-wins writers of
        // settings.json; let the first instance win and exit this one quietly. Deliberately not
        // applied to --settings above: that's an explicit, short-lived window, and gating it
        // would make it silently do nothing while the tray runs.
        using var instanceLock = new Mutex(initiallyOwned: true, @"Local\HueBar.SingleInstance", out bool isFirstInstance);
        if (!isFirstInstance)
            return;

        Application.Run(new TrayApplicationContext());
    }
}
