using System.Threading;
using System.Windows;
using System.Windows.Media;
using HueBar.Core;

namespace HueBar;

/// <summary>
/// The connect pane (WPF): discover a bridge (or type its IP), then pair by pressing the
/// bridge's physical link button. On success the application key is saved to
/// <see cref="AppSettings"/>.
///
/// Rendered inside the WinForms tray shell via <c>ElementHost</c> (see <see cref="SettingsForm"/>).
/// Hosting on the app's single WinForms UI thread means these async handlers resume on that
/// thread, so touching the WPF controls after each <c>await</c> is safe.
/// </summary>
public partial class SettingsView : System.Windows.Controls.UserControl
{
    private readonly HueClient _hue;
    private readonly AppSettings _settings;
    private CancellationTokenSource? _cts;

    /// <summary>Raised when the status text changes (which can grow the content height so the
    /// host form needs to refit). See <see cref="SettingsForm"/>.</summary>
    public event EventHandler? ContentSizeChanged;

    public SettingsView(HueClient hue, AppSettings settings)
    {
        _hue = hue;
        _settings = settings;

        InitializeComponent();

        // Paint with the OS's current light/dark preference. WPF has no built-in dark theme, so the
        // brushes the XAML references via DynamicResource are supplied here from the Core palette.
        ApplyTheme(SystemThemeReader.Current());

        _ipBox.Text = _settings.BridgeIp ?? "";
        UpdateConnectedStatus();
    }

    /// <summary>
    /// Injects the palette for <paramref name="theme"/> into this control's resource dictionary,
    /// under the keys the XAML styles reference with <c>DynamicResource</c>. The theme decision and
    /// the colours themselves live in HueBar.Core (and are unit-tested); this just turns hex into
    /// <see cref="SolidColorBrush"/>es WPF can paint with.
    /// </summary>
    private void ApplyTheme(AppTheme theme)
    {
        var palette = ThemePalette.For(theme);

        Resources["BackgroundBrush"] = Brush(palette.Background);
        Resources["TextBrush"] = Brush(palette.Text);
        Resources["SubtleTextBrush"] = Brush(palette.SubtleText);
        Resources["AccentBrush"] = Brush(palette.Accent);
        Resources["AccentHoverBrush"] = Brush(palette.AccentHover);
        Resources["AccentPressedBrush"] = Brush(palette.AccentPressed);
        Resources["OnAccentTextBrush"] = Brush(palette.OnAccentText);
        Resources["ControlBorderBrush"] = Brush(palette.ControlBorder);
        Resources["ControlFillBrush"] = Brush(palette.ControlFill);
        Resources["ControlHoverFillBrush"] = Brush(palette.ControlHoverFill);
        Resources["ControlPressedFillBrush"] = Brush(palette.ControlPressedFill);
        Resources["TextBoxBackgroundBrush"] = Brush(palette.TextBoxBackground);
    }

    private static SolidColorBrush Brush(string hex)
    {
        // Fully qualified: both System.Drawing and System.Windows.Media are in scope in this
        // WinForms-hosting project, so bare Color/ColorConverter would be ambiguous.
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void UpdateConnectedStatus()
    {
        if (_settings.IsConnected)
            SetStatus($"Currently connected to bridge at {_settings.BridgeIp}.");
    }

    private async void OnDiscover(object sender, RoutedEventArgs e)
    {
        _discoverButton.IsEnabled = false;
        SetStatus("Searching for bridges on your network…");
        try
        {
            var bridges = await _hue.DiscoverBridgesAsync();
            if (bridges.Count == 0)
            {
                SetStatus("No bridges found automatically. Enter the bridge's IP address manually.");
            }
            else
            {
                _ipBox.Text = bridges[0].InternalIpAddress ?? "";
                SetStatus(bridges.Count == 1
                    ? $"Found a bridge at {_ipBox.Text}."
                    : $"Found {bridges.Count} bridges; using {_ipBox.Text}. Edit the IP if this is the wrong one.");
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Discovery failed: {ex.Message}");
        }
        finally
        {
            _discoverButton.IsEnabled = true;
        }
    }

    private async void OnConnect(object sender, RoutedEventArgs e)
    {
        var ip = _ipBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(ip))
        {
            SetStatus("Enter or discover a bridge IP address first.");
            return;
        }

        _connectButton.IsEnabled = false;
        _discoverButton.IsEnabled = false;
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        SetStatus("Press the link button on top of your Hue bridge now… waiting up to 30 seconds.");

        try
        {
            // The retry/timeout/link-button loop lives in HueBar.Core.BridgePairing (unit-tested);
            // here we just drive it with the real client + clock and map the outcome to status text.
            var outcome = await BridgePairing.RunAsync(
                ct => _hue.PairAsync(ip, ct: ct),
                ct => Task.Delay(1500, ct),
                _cts.Token);

            switch (outcome.Status)
            {
                case PairingStatus.Connected:
                    _settings.BridgeIp = ip;
                    _settings.Username = outcome.Username;
                    _settings.Save();
                    SetStatus("Connected! Right-click the tray icon to pick a room and scene. You can close this window.");
                    break;

                case PairingStatus.BridgeError:
                    SetStatus($"Bridge error: {outcome.ErrorMessage}");
                    break;

                case PairingStatus.TimedOut:
                    SetStatus("Timed out waiting for the link button. Press it, then click Connect again.");
                    break;
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Connection failed: {ex.Message}. Check the IP address and that the bridge is reachable.");
        }
        finally
        {
            _connectButton.IsEnabled = true;
            _discoverButton.IsEnabled = true;
        }
    }

    private void SetStatus(string text)
    {
        _statusLabel.Text = text;
        ContentSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Cancels any in-flight pairing wait; called by the host form when it closes.</summary>
    public void CancelPending()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }
}
