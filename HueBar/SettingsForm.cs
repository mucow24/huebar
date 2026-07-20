using System.Runtime.InteropServices;
using System.Windows.Forms.Integration;
using HueBar.Core;

namespace HueBar;

/// <summary>
/// Thin WinForms shell that hosts the WPF <see cref="SettingsView"/> connect pane via
/// <see cref="ElementHost"/>. The visible UI is WPF (modern Fluent styling); keeping the host a
/// plain <see cref="Form"/> means the tray context and the <c>--settings</c> entry point stay
/// unchanged, and the WinForms message loop / synchronization context guarantee the view's async
/// discover &amp; pair operations resume on the single UI thread.
///
/// The form sizes itself to the WPF content (which is fixed-width and grows in height as a longer
/// status message wraps), so nothing is clipped regardless of DPI.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly SettingsView _view;
    private readonly ElementHost _host;
    private readonly AppTheme _theme;

    public SettingsForm(HueClient hue, AppSettings settings)
    {
        _view = new SettingsView(hue, settings);

        // Match the OS light/dark preference. The WPF content themes itself (see SettingsView);
        // here we only need the host chrome — the client background behind/around the ElementHost,
        // and the title bar (via DWM in OnHandleCreated) — to match so nothing flashes white.
        _theme = SystemThemeReader.Current();
        var chrome = ColorTranslator.FromHtml(ThemePalette.For(_theme).Background);

        Text = "HueBar — Settings";
        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = IconFactory.CreateBulbIcon();
        BackColor = chrome;

        _host = new ElementHost
        {
            Dock = DockStyle.Fill,
            BackColor = chrome,
            Child = _view,
        };
        // A longer status wraps to more lines, growing the WPF content; refit so it stays visible.
        _view.ContentSizeChanged += (_, _) => FitToContent();

        Controls.Add(_host);
    }

    // Win32 title bars are light by default; ask DWM to paint this window's title bar dark when the
    // OS is in dark mode. Supported on Windows 10 20H1+ / 11; on anything older the call is a no-op
    // and the default (light) title bar is used.
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int pvAttribute, int cbAttribute);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int useDark = _theme == AppTheme.Dark ? 1 : 0;
            DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        }
        catch
        {
            // dwmapi unavailable / attribute unsupported: leave the default title bar.
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitToContent();
    }

    /// <summary>
    /// Sizes the form to exactly fit the hosted WPF content so nothing is clipped. The content is
    /// fixed-width and grows/shrinks in height (a longer status wraps; forgetting a bridge shortens
    /// the list), so we measure it unconstrained (avoiding ElementHost's AutoSize, which clamps the
    /// child's width and under-reports it) and convert WPF's device-independent units to physical
    /// pixels for <see cref="Form.ClientSize"/>. We refit exactly every time rather than latching a
    /// minimum, so the window tracks the content in both directions.
    /// </summary>
    private void FitToContent()
    {
        _view.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = _view.DesiredSize; // 1/96" device-independent units
        double scale = DeviceDpi / 96.0;
        ClientSize = new Size(
            (int)Math.Ceiling(desired.Width * scale),
            (int)Math.Ceiling(desired.Height * scale));
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _view.CancelPending();
        base.OnFormClosed(e);
    }
}
