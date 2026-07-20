using HueBar.Core;
using Microsoft.Win32;

namespace HueBar;

/// <summary>
/// Reads the current Windows app-theme preference from the registry. This is the platform glue
/// that can't run headlessly; the actual interpretation of the value lives in (and is tested via)
/// <see cref="SystemTheme.FromRegistryValue"/> in HueBar.Core.
/// </summary>
internal static class SystemThemeReader
{
    private const string PersonalizeKey =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>The OS app theme right now, defaulting to <see cref="AppTheme.Light"/> if the
    /// preference can't be read for any reason.</summary>
    public static AppTheme Current()
    {
        try
        {
            // Null default => absent key is reported as "no value", which Core maps to Light.
            var raw = Registry.GetValue(PersonalizeKey, "AppsUseLightTheme", null);
            return SystemTheme.FromRegistryValue(raw);
        }
        catch
        {
            return AppTheme.Light;
        }
    }
}
