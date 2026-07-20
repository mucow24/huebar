using System.Threading;
using System.Windows;
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

        _ipBox.Text = _settings.BridgeIp ?? "";
        UpdateConnectedStatus();
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
            while (!_cts.IsCancellationRequested)
            {
                var result = await _hue.PairAsync(ip, ct: _cts.Token);

                if (result.Success)
                {
                    _settings.BridgeIp = ip;
                    _settings.Username = result.Username;
                    _settings.Save();
                    SetStatus("Connected! Right-click the tray icon to pick a room and scene. You can close this window.");
                    return;
                }

                if (!result.LinkButtonNotPressed)
                {
                    SetStatus($"Bridge error: {result.ErrorMessage}");
                    return;
                }

                await Task.Delay(1500, _cts.Token);
            }

            SetStatus("Timed out waiting for the link button. Press it, then click Connect again.");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Timed out. Press the link button, then click Connect again.");
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
