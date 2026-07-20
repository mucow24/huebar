using System.Reflection;
using HueBar.Core;

namespace HueBar;

/// <summary>
/// Owns the tray icon and its lifecycle. Builds the room/scene menu on demand from cached
/// bridge data, refreshes that data in the background, and routes scene clicks to the bridge.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _menu;
    private readonly HueClient _hue = new();

    private AppSettings _settings;
    private List<Room> _rooms = new();
    private SettingsForm? _settingsForm;
    private bool _refreshing;

    public TrayApplicationContext()
    {
        _settings = AppSettings.Load();

        _menu = new ContextMenuStrip();
        _menu.Opening += (_, _) =>
        {
            // Show the menu immediately from cached data, then refresh in the background
            // so the *next* open reflects any rooms/scenes changed in the Hue app.
            BuildMenu();
            _ = RefreshInBackgroundAsync();
        };

        _notifyIcon = new NotifyIcon
        {
            Icon = IconFactory.CreateBulbIcon(),
            Text = "HueBar — Philips Hue control",
            Visible = true,
            ContextMenuStrip = _menu,
        };

        // Right-click shows the menu natively; mirror that on left-click for one-click access.
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                ShowMenuAtCursor();
        };

        if (_settings.IsConnected)
            _ = RefreshRoomsAsync();
        else
            ShowSettings(); // First run: guide the user to connect.
    }

    /// <summary>Reuses NotifyIcon's own menu-display so the menu dismisses correctly on click-away.</summary>
    private void ShowMenuAtCursor()
    {
        var method = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
        method?.Invoke(_notifyIcon, null);
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();

        if (!_settings.IsConnected)
        {
            _menu.Items.Add(Disabled("Not connected to a bridge"));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Connect to bridge…", null, (_, _) => ShowSettings());
        }
        else if (_rooms.Count == 0)
        {
            _menu.Items.Add(Disabled("No rooms found"));
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Refresh", null, async (_, _) => await RefreshRoomsAsync());
            _menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        }
        else
        {
            foreach (var room in _rooms)
                _menu.Items.Add(BuildRoomItem(room));

            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add("Refresh rooms & scenes", null, async (_, _) => await RefreshRoomsAsync());
            _menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        }

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Exit", null, (_, _) => ExitApp());
    }

    private ToolStripMenuItem BuildRoomItem(Room room)
    {
        var roomItem = new ToolStripMenuItem(room.Name);

        if (room.Scenes.Count == 0)
        {
            roomItem.DropDownItems.Add(Disabled("(no scenes)"));
        }
        else
        {
            foreach (var scene in room.Scenes)
            {
                var sceneItem = new ToolStripMenuItem(scene.Name);
                // Capture by local to avoid the closed-over-loop-variable trap.
                var r = room;
                var sc = scene;
                sceneItem.Click += async (_, _) => await ActivateAsync(r, sc);
                roomItem.DropDownItems.Add(sceneItem);
            }
        }

        // "All off" at the bottom of every room, to switch the whole room off.
        roomItem.DropDownItems.Add(new ToolStripSeparator());
        var offItem = new ToolStripMenuItem("All off");
        var roomForOff = room;
        offItem.Click += async (_, _) => await TurnRoomOffAsync(roomForOff);
        roomItem.DropDownItems.Add(offItem);

        return roomItem;
    }

    private static ToolStripMenuItem Disabled(string text) => new(text) { Enabled = false };

    private async Task ActivateAsync(Room room, SceneRef scene)
    {
        var bridge = _settings.ActiveBridge;
        if (bridge is not { IsUsable: true })
            return;
        try
        {
            bool ok = await _hue.ActivateSceneAsync(bridge.BridgeIp, bridge.Username, room.Id, scene.Id);
            if (!ok)
                Notify($"Couldn't activate \"{scene.Name}\".", ToolTipIcon.Warning);
        }
        catch (Exception ex)
        {
            Notify($"Error activating \"{scene.Name}\": {ex.Message}", ToolTipIcon.Error);
        }
    }

    private async Task TurnRoomOffAsync(Room room)
    {
        var bridge = _settings.ActiveBridge;
        if (bridge is not { IsUsable: true })
            return;
        try
        {
            bool ok = await _hue.TurnGroupOffAsync(bridge.BridgeIp, bridge.Username, room.Id);
            if (!ok)
                Notify($"Couldn't turn off \"{room.Name}\".", ToolTipIcon.Warning);
        }
        catch (Exception ex)
        {
            Notify($"Error turning off \"{room.Name}\": {ex.Message}", ToolTipIcon.Error);
        }
    }

    private async Task RefreshInBackgroundAsync()
    {
        if (_refreshing || !_settings.IsConnected)
            return;
        _refreshing = true;
        try
        {
            await RefreshRoomsAsync();
        }
        finally
        {
            _refreshing = false;
        }
    }

    private async Task RefreshRoomsAsync()
    {
        var bridge = _settings.ActiveBridge;
        if (bridge is not { IsUsable: true })
            return;
        try
        {
            var groups = await _hue.GetGroupsAsync(bridge.BridgeIp, bridge.Username);
            var scenes = await _hue.GetScenesAsync(bridge.BridgeIp, bridge.Username);
            _rooms = RoomSceneMapper.BuildRooms(groups, scenes, _settings.IncludeZones);
        }
        catch (Exception ex)
        {
            Notify($"Failed to load rooms: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_hue, _settings);
        _settingsForm.FormClosed += async (_, _) =>
        {
            _settingsForm = null;
            // Settings may have changed (new pairing); reload and refresh the menu data.
            _settings = AppSettings.Load();
            if (_settings.IsConnected)
                await RefreshRoomsAsync();
        };
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    private void Notify(string message, ToolTipIcon icon) =>
        _notifyIcon.ShowBalloonTip(3000, "HueBar", message, icon);

    private void ExitApp()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ExitThread();
    }
}
