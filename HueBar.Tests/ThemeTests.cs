using System.Reflection;
using System.Text.RegularExpressions;
using HueBar.Core;

namespace HueBar.Tests;

public class SystemThemeTests
{
    // The contract of the Windows AppsUseLightTheme DWORD: only an explicit 0 means dark; a 1,
    // a missing value (null), or anything of an unexpected type is treated as light — exactly how
    // Windows itself falls back. Registry.GetValue hands us the value boxed as object, so the
    // mapping has to cope with the wrong-type cases too.
    [Theory]
    [InlineData(0, AppTheme.Dark)]
    [InlineData(1, AppTheme.Light)]
    [InlineData(null, AppTheme.Light)]     // key absent
    [InlineData(2, AppTheme.Light)]        // unexpected number
    [InlineData("0", AppTheme.Light)]      // unexpected type: a string, not a DWORD
    public void FromRegistryValue_maps_the_apps_use_light_theme_dword(object? raw, AppTheme expected)
    {
        Assert.Equal(expected, SystemTheme.FromRegistryValue(raw));
    }
}

public class ThemePaletteTests
{
    private static readonly Regex HexColor = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    [Fact]
    public void For_selects_the_matching_palette()
    {
        Assert.Same(ThemePalette.Light, ThemePalette.For(AppTheme.Light));
        Assert.Same(ThemePalette.Dark, ThemePalette.For(AppTheme.Dark));
    }

    // Characterization / regression guard. The window shipped with these exact light colours before
    // it became theme-aware; moving the UI over to this palette must not change the light look.
    // The literal is duplicated on purpose — if someone edits a light colour, this disagrees and
    // forces the change to be intentional.
    [Fact]
    public void Light_palette_still_matches_the_colours_the_window_shipped_with()
    {
        var shipped = new ThemePalette
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

        Assert.Equal(shipped, ThemePalette.Light);
    }

    [Fact]
    public void The_dark_theme_is_dark_and_the_light_theme_is_light()
    {
        // The whole point of the feature: dark mode must actually be dark, light must stay light.
        Assert.True(RelativeLuminance(ThemePalette.Dark.Background) < 0.2,
            $"dark background {ThemePalette.Dark.Background} is not dark");
        Assert.True(RelativeLuminance(ThemePalette.Light.Background) > 0.8,
            $"light background {ThemePalette.Light.Background} is not light");
    }

    [Theory]
    [InlineData(nameof(AppTheme.Light))]
    [InlineData(nameof(AppTheme.Dark))]
    public void Text_is_readable_against_its_backgrounds_in_both_themes(string themeName)
    {
        var palette = ThemePalette.For(Enum.Parse<AppTheme>(themeName));

        // WCAG AA for normal text is 4.5:1. Body text on the window, and the primary button's
        // label on the accent fill, both have to clear it — otherwise the theme is unreadable.
        AssertContrastAtLeast(4.5, palette.Text, palette.Background, "body text vs background");
        AssertContrastAtLeast(4.5, palette.OnAccentText, palette.Accent, "button label vs accent");
    }

    [Theory]
    [InlineData(nameof(AppTheme.Light))]
    [InlineData(nameof(AppTheme.Dark))]
    public void Every_colour_is_a_valid_rrggbb_hex_string(string themeName)
    {
        var palette = ThemePalette.For(Enum.Parse<AppTheme>(themeName));

        foreach (var (name, value) in Colors(palette))
            Assert.True(HexColor.IsMatch(value), $"{themeName}.{name} = \"{value}\" is not #RRGGBB");
    }

    // ---- helpers --------------------------------------------------------------

    private static void AssertContrastAtLeast(double minimum, string foreground, string background, string what)
    {
        var ratio = ContrastRatio(foreground, background);
        Assert.True(ratio >= minimum,
            $"{what}: contrast {ratio:0.00}:1 between {foreground} and {background} is below {minimum}:1");
    }

    private static IEnumerable<(string Name, string Value)> Colors(ThemePalette palette) =>
        typeof(ThemePalette)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => (p.Name, (string)p.GetValue(palette)!));

    private static double ContrastRatio(string a, string b)
    {
        double la = RelativeLuminance(a), lb = RelativeLuminance(b);
        double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
        return (hi + 0.05) / (lo + 0.05);
    }

    // WCAG relative luminance from an #RRGGBB string.
    private static double RelativeLuminance(string hex)
    {
        var (r, g, b) = Parse(hex);
        static double Linear(int channel)
        {
            double c = channel / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Linear(r) + 0.7152 * Linear(g) + 0.0722 * Linear(b);
    }

    private static (int R, int G, int B) Parse(string hex) => (
        Convert.ToInt32(hex.Substring(1, 2), 16),
        Convert.ToInt32(hex.Substring(3, 2), 16),
        Convert.ToInt32(hex.Substring(5, 2), 16));
}
