using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using HueBar.Core;

// This project references both WinForms and WPF, so these type names are ambiguous; pin them to WPF
// (the rows we build in code-behind are WPF controls painted with WPF brushes).
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using RadioButton = System.Windows.Controls.RadioButton;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace HueBar;

/// <summary>
/// The settings pane (WPF). Lists the bridges HueBar has paired with and lets you switch the
/// active one (instant — no link-button press, because the app key is already stored), add another
/// bridge (discover or type its IP, then pair once), forget one, and toggle whether zones appear
/// in the tray menu. On any change the (shared) <see cref="AppSettings"/> is saved to disk; the
/// tray reloads it when this window closes.
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

    /// <summary>Raised when the content height changes (list re-rendered, or status text wrapped),
    /// so the host form can refit. See <see cref="SettingsForm"/>.</summary>
    public event EventHandler? ContentSizeChanged;

    public SettingsView(HueClient hue, AppSettings settings)
    {
        _hue = hue;
        _settings = settings;

        InitializeComponent();

        // Paint with the OS's current light/dark preference. WPF has no built-in dark theme, so the
        // brushes the XAML references via DynamicResource are supplied here from the Core palette.
        ApplyTheme(SystemThemeReader.Current());

        _includeZones.IsChecked = _settings.IncludeZones;
        RenderBridges();

        if (_settings.IsConnected)
            SetStatus($"Controlling {_settings.ActiveBridge!.DisplayName}. Switch bridges above, or add another below.");
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

    // ---- Bridge list ----------------------------------------------------------

    /// <summary>Rebuilds the bridge rows from the current settings and refits the host form.</summary>
    private void RenderBridges()
    {
        _bridgeList.Children.Clear();

        _emptyBridges.Visibility = _settings.Bridges.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var bridge in _settings.Bridges)
            _bridgeList.Children.Add(BuildBridgeRow(bridge));

        ContentSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private UIElement BuildBridgeRow(BridgeEntry bridge)
    {
        bool isActive = bridge.Id == _settings.ActiveBridgeId;

        var text = new StackPanel();
        text.Children.Add(new TextBlock
        {
            Text = bridge.DisplayName,
            FontSize = 14.5,
            Foreground = (Brush)Resources["TextBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        // Show the IP on its own line only when the primary label is a *name* (else it would repeat).
        if (!string.Equals(bridge.DisplayName, bridge.BridgeIp, StringComparison.Ordinal))
        {
            text.Children.Add(new TextBlock
            {
                Text = bridge.BridgeIp,
                FontSize = 12,
                Margin = new Thickness(0, 1, 0, 0),
                Foreground = (Brush)Resources["SubtleTextBrush"],
            });
        }

        var radio = new RadioButton
        {
            GroupName = "bridges",
            Style = (Style)FindResource("BridgeRadio"),
            Content = text,
            IsChecked = isActive,
            Tag = bridge.Id,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // Attached AFTER IsChecked is set above, so re-rendering never re-fires this as a "switch".
        radio.Checked += OnBridgeChecked;
        Grid.SetColumn(radio, 0);

        var forget = new Button
        {
            Content = "Forget",
            Style = (Style)FindResource("SubtleButton"),
            Tag = bridge.Id,
            VerticalAlignment = VerticalAlignment.Center,
        };
        forget.Click += OnForget;
        Grid.SetColumn(forget, 1);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(radio);
        grid.Children.Add(forget);

        return new Border
        {
            Padding = new Thickness(8, 8, 6, 8),
            CornerRadius = new CornerRadius(6),
            Background = isActive ? (Brush)Resources["ControlHoverFillBrush"] : Brushes.Transparent,
            Child = grid,
        };
    }

    private void OnBridgeChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { Tag: string id } && id != _settings.ActiveBridgeId)
        {
            _settings.SetActiveBridge(id);
            _settings.Save();
            RenderBridges(); // move the active-row highlight to the new selection
            SetStatus($"Switched to {_settings.ActiveBridge?.DisplayName}.");
        }
    }

    private void OnForget(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id })
        {
            _settings.RemoveBridge(id);
            _settings.Save();
            RenderBridges();
            SetStatus(_settings.Bridges.Count == 0
                ? "All bridges removed. Add one below to reconnect."
                : $"Bridge forgotten. Now controlling {_settings.ActiveBridge?.DisplayName}.");
        }
    }

    private void OnIncludeZonesChanged(object sender, RoutedEventArgs e)
    {
        _settings.IncludeZones = _includeZones.IsChecked == true;
        _settings.Save();
    }

    // ---- Add a bridge (discover + pair) --------------------------------------

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
                    var username = outcome.Username ?? "";
                    // Best-effort enrichment: the bridge's own name gives the list a friendly label,
                    // and its bridgeid a stable key. If the bridge doesn't answer, fall back to the
                    // IP as the id and no name — pairing already succeeded, so never fail on this.
                    var config = await _hue.GetBridgeConfigAsync(ip, username);
                    var id = !string.IsNullOrWhiteSpace(config?.BridgeId) ? config!.BridgeId! : ip;
                    _settings.AddOrUpdateBridge(id, ip, username, config?.Name);
                    _settings.Save();

                    _ipBox.Text = "";
                    RenderBridges();
                    SetStatus($"Connected to {_settings.ActiveBridge!.DisplayName}. Add another bridge, or close this window.");
                    break;

                case PairingStatus.BridgeError:
                    SetStatus($"Bridge error: {outcome.ErrorMessage}");
                    break;

                case PairingStatus.TimedOut:
                    SetStatus("Timed out waiting for the link button. Press it, then click Connect again.");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Not the pairing deadline (BridgePairing folds that into TimedOut) — this is the
            // HTTP request itself giving up because nothing answered at that address.
            SetStatus("The bridge did not respond. Check the IP address and that the bridge is reachable.");
        }
        catch (Exception ex)
        {
            SetStatus($"Connection failed: {ex.Message}. Check the IP address and that the bridge is reachable.");
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
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
