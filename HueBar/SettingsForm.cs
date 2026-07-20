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

    public SettingsForm(HueClient hue, AppSettings settings)
    {
        _view = new SettingsView(hue, settings);

        Text = "HueBar — Connect to Bridge";
        AutoScaleMode = AutoScaleMode.Font;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = IconFactory.CreateBulbIcon();
        BackColor = Color.White;

        _host = new ElementHost
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Child = _view,
        };
        // A longer status wraps to more lines, growing the WPF content; refit so it stays visible.
        _view.ContentSizeChanged += (_, _) => FitToContent();

        Controls.Add(_host);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitToContent();
    }

    /// <summary>
    /// Sizes the form to exactly fit the hosted WPF content so nothing is clipped. The content is
    /// fixed-width and grows only in height, so we measure it unconstrained (avoiding ElementHost's
    /// AutoSize, which clamps the child's width and under-reports it) and convert WPF's
    /// device-independent units to physical pixels for <see cref="Form.ClientSize"/>.
    /// </summary>
    private void FitToContent()
    {
        _view.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
        var desired = _view.DesiredSize; // 1/96" device-independent units
        double scale = DeviceDpi / 96.0;
        var size = new Size(
            (int)Math.Ceiling(desired.Width * scale),
            (int)Math.Ceiling(desired.Height * scale));

        ClientSize = size;
        if (MinimumSize.IsEmpty)
            MinimumSize = Size; // don't let a later (shorter) status shrink it below the initial fit
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _view.CancelPending();
        base.OnFormClosed(e);
    }
}
