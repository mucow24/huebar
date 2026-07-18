using HueBar.Core;

namespace HueBar;

/// <summary>
/// The connect pane: discover a bridge (or type its IP), then pair by pressing the bridge's
/// physical link button. On success the application key is saved to <see cref="AppSettings"/>.
///
/// The layout is fully auto-sizing (a <see cref="TableLayoutPanel"/> of AutoSize controls) and
/// DPI/font aware, so nothing is clipped regardless of Windows display scaling or system font
/// size — the form grows to fit its content and long messages wrap.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly HueClient _hue;
    private readonly AppSettings _settings;

    private readonly TableLayoutPanel _layout;
    private readonly TextBox _ipBox;
    private readonly Button _discoverButton;
    private readonly Button _connectButton;
    private readonly Label _statusLabel;
    private CancellationTokenSource? _cts;

    public SettingsForm(HueClient hue, AppSettings settings)
    {
        _hue = hue;
        _settings = settings;

        // Wrap width for the multi-line labels, derived from the font so it scales with DPI.
        int wrapWidth = Font.Height * 22;

        Text = "HueBar — Connect to Bridge";
        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = IconFactory.CreateBulbIcon();

        var title = new Label
        {
            Text = "Connect HueBar to your Philips Hue bridge",
            Font = new Font(Font.FontFamily, Font.Size + 1.5f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(3, 3, 3, 10),
            Anchor = AnchorStyles.Left,
        };

        var ipLabel = new Label
        {
            Text = "Bridge IP address:",
            AutoSize = true,
            Anchor = AnchorStyles.Left, // left-aligned, vertically centered in its cell
        };

        _ipBox = new TextBox
        {
            Text = _settings.BridgeIp ?? "",
            MinimumSize = new Size(Font.Height * 11, 0),
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
        };

        _discoverButton = new Button
        {
            Text = "Discover",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 3, 8, 3),
            Margin = new Padding(6, 3, 3, 3),
            Anchor = AnchorStyles.Left,
        };
        _discoverButton.Click += OnDiscover;

        var hint = new Label
        {
            Text = "Press the link button on top of the bridge, then click Connect.",
            AutoSize = true,
            MaximumSize = new Size(wrapWidth, 0),
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 10, 3, 10),
            Anchor = AnchorStyles.Left,
        };

        _connectButton = new Button
        {
            Text = "Connect",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 5, 14, 5),
            Margin = new Padding(0, 3, 8, 3),
        };
        _connectButton.Click += OnConnect;

        var closeButton = new Button
        {
            Text = "Close",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14, 5, 14, 5),
            Margin = new Padding(0, 3, 0, 3),
            DialogResult = DialogResult.Cancel,
        };
        closeButton.Click += (_, _) => Close();

        var buttonRow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 4, 0, 4),
            Anchor = AnchorStyles.Left,
        };
        buttonRow.Controls.Add(_connectButton);
        buttonRow.Controls.Add(closeButton);

        _statusLabel = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(wrapWidth, 0),
            Margin = new Padding(3, 6, 3, 3),
            Anchor = AnchorStyles.Left,
        };

        // The panel AutoSizes to its content; the form is then sized to the panel's measured
        // PreferredSize in OnLoad (Form.AutoSize proved unreliable under PerMonitorV2 here).
        var layout = _layout = new TableLayoutPanel
        {
            Location = new Point(0, 0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(14),
            ColumnCount = 3,
            RowCount = 5,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int i = 0; i < 5; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(title, 0, 0);
        layout.SetColumnSpan(title, 3);

        layout.Controls.Add(ipLabel, 0, 1);
        layout.Controls.Add(_ipBox, 1, 1);
        layout.Controls.Add(_discoverButton, 2, 1);

        layout.Controls.Add(hint, 0, 2);
        layout.SetColumnSpan(hint, 3);

        layout.Controls.Add(buttonRow, 0, 3);
        layout.SetColumnSpan(buttonRow, 3);

        layout.Controls.Add(_statusLabel, 0, 4);
        layout.SetColumnSpan(_statusLabel, 3);

        Controls.Add(layout);
        CancelButton = closeButton;

        UpdateConnectedStatus();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitToContent();
    }

    /// <summary>Sizes the form to exactly fit the layout, so nothing is ever clipped.</summary>
    private void FitToContent()
    {
        ClientSize = _layout.PreferredSize;
        if (MinimumSize.IsEmpty)
            MinimumSize = Size; // don't let a later (shorter) status shrink it below the initial fit
    }

    private void UpdateConnectedStatus()
    {
        if (_settings.IsConnected)
            SetStatus($"Currently connected to bridge at {_settings.BridgeIp}.");
    }

    private async void OnDiscover(object? sender, EventArgs e)
    {
        _discoverButton.Enabled = false;
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
            _discoverButton.Enabled = true;
        }
    }

    private async void OnConnect(object? sender, EventArgs e)
    {
        var ip = _ipBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(ip))
        {
            SetStatus("Enter or discover a bridge IP address first.");
            return;
        }

        _connectButton.Enabled = false;
        _discoverButton.Enabled = false;
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
            _connectButton.Enabled = true;
            _discoverButton.Enabled = true;
        }
    }

    private void SetStatus(string text)
    {
        _statusLabel.Text = text;
        // A longer message may wrap to more lines; regrow the form so it stays fully visible.
        if (IsHandleCreated)
            FitToContent();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        base.OnFormClosed(e);
    }
}
