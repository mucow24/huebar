namespace HueBar.Core;

/// <summary>The app-level colour theme the settings window paints itself with.</summary>
public enum AppTheme
{
    Light,
    Dark,
}

/// <summary>
/// Interprets the operating system's app-theme preference. Windows records it under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme</c>
/// as a DWORD: <c>1</c> = light, <c>0</c> = dark, absent = light (the OS default).
///
/// The registry <em>read</em> is platform glue and lives in the UI layer; this pure mapping —
/// the part with an actual contract worth pinning — lives here so it can be unit-tested headlessly.
/// </summary>
public static class SystemTheme
{
    /// <summary>
    /// Maps a raw <c>AppsUseLightTheme</c> registry value (as returned boxed by
    /// <c>Registry.GetValue</c>) to an <see cref="AppTheme"/>. Only an explicit <c>0</c> means
    /// dark; anything else — <c>1</c>, a missing value (<see langword="null"/>), or an unexpected
    /// type — falls back to <see cref="AppTheme.Light"/>, matching how Windows treats the key.
    /// </summary>
    public static AppTheme FromRegistryValue(object? appsUseLightTheme) =>
        appsUseLightTheme is int value && value == 0 ? AppTheme.Dark : AppTheme.Light;
}

/// <summary>
/// The full set of colours the settings window needs, for one theme. Colours are plain
/// <c>#RRGGBB</c> hex strings so this stays free of any WPF/WinForms dependency; the UI layer
/// converts them to the brush type it needs. Keys mirror the <c>DynamicResource</c> names used in
/// <c>SettingsView.xaml</c>.
/// </summary>
public sealed record ThemePalette
{
    /// <summary>Window / surface background.</summary>
    public required string Background { get; init; }

    /// <summary>Primary text.</summary>
    public required string Text { get; init; }

    /// <summary>Secondary / status text.</summary>
    public required string SubtleText { get; init; }

    /// <summary>Accent fill (primary button, focus border).</summary>
    public required string Accent { get; init; }

    public required string AccentHover { get; init; }
    public required string AccentPressed { get; init; }

    /// <summary>Text/foreground drawn on top of the accent (e.g. the primary button label).</summary>
    public required string OnAccentText { get; init; }

    public required string ControlBorder { get; init; }
    public required string ControlFill { get; init; }
    public required string ControlHoverFill { get; init; }
    public required string ControlPressedFill { get; init; }

    /// <summary>Text-input background.</summary>
    public required string TextBoxBackground { get; init; }

    /// <summary>The palette for the given theme.</summary>
    public static ThemePalette For(AppTheme theme) => theme == AppTheme.Dark ? Dark : Light;

    /// <summary>
    /// Windows 11 light Fluent palette. These are the exact colours the window shipped with before
    /// it became theme-aware, so switching the UI over to this palette leaves the light look
    /// unchanged (pinned by <c>ThemePaletteTests</c>).
    /// </summary>
    public static ThemePalette Light { get; } = new()
    {
        Background = "#FFFFFF",
        Text = "#1B1B1B",
        SubtleText = "#5F5F5F",
        Accent = "#0067C0",
        AccentHover = "#1976CF",
        AccentPressed = "#005AA8",
        OnAccentText = "#FFFFFF",
        ControlBorder = "#C4C4C4",
        ControlFill = "#FBFBFB",
        ControlHoverFill = "#F0F0F0",
        ControlPressedFill = "#E8E8E8",
        TextBoxBackground = "#FFFFFF",
    };

    /// <summary>Windows 11 dark Fluent palette (dark surface, near-white text, light-blue accent
    /// with dark text on top).</summary>
    public static ThemePalette Dark { get; } = new()
    {
        Background = "#202020",
        Text = "#F5F5F5",
        SubtleText = "#A8A8A8",
        Accent = "#4CC2FF",
        AccentHover = "#63CBFF",
        AccentPressed = "#3AAEEB",
        OnAccentText = "#000000",
        ControlBorder = "#3D3D3D",
        ControlFill = "#2D2D2D",
        ControlHoverFill = "#383838",
        ControlPressedFill = "#272727",
        TextBoxBackground = "#2D2D2D",
    };
}
