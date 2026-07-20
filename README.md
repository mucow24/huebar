# HueBar

A tiny Windows system-tray app for controlling Philips Hue lights. A white lightbulb icon
sits in your notification area; click it to pick a **room** → **scene**.

## Features (MVP)

- **Tray icon** — a simple white lightbulb, drawn at runtime (no image asset to ship).
- **Bridge setup pane** — auto-discover your bridge (or type its IP), then pair by pressing
  the bridge's physical link button. The credential is saved to `%AppData%\HueBar\settings.json`.
- **Rooms → scenes menu** — left- or right-click the tray icon to get a menu of your rooms
  (and zones). Each room expands to its scenes; picking one activates it on the bridge.
- **Light/dark** — the connect window follows your Windows app-theme setting (Settings →
  Personalization → Colors → *Choose your mode*), title bar included.

## Requirements

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (or build/run with the SDK)
- A Philips Hue bridge on the same network

## Project layout

| Project          | Target           | Purpose                                                        |
|------------------|------------------|----------------------------------------------------------------|
| `HueBar.Core`    | `net8.0`         | Hue v1 API client, models, settings, and the room/scene mapper |
| `HueBar`         | `net8.0-windows` | WinForms tray app (icon, menu, settings pane)                  |
| `HueBar.Tests`   | `net8.0`         | Dependency-free test runner for the mapping logic              |

The API/mapping logic lives in `HueBar.Core` with **no** WinForms dependency, so it can be
unit-tested headlessly.

## Build & run

```powershell
dotnet build HueBar.sln
dotnet run --project HueBar          # launches the tray app
dotnet test HueBar.sln               # runs the test suite (xUnit)
```

## Testing

The bridge/client/settings logic lives in `HueBar.Core` and is covered by an xUnit suite
in `HueBar.Tests`. The suite is a **required check** on `main` — PRs can't merge until it
passes. See [`TESTING.md`](TESTING.md) for the policy and what is (and isn't) tested.

## First run

1. The **Connect to Bridge** window opens automatically.
2. Click **Discover** (or type the bridge IP), then **press the link button on top of your
   bridge** and click **Connect** within ~30 seconds.
3. Once connected, left- or right-click the tray icon to browse rooms and scenes.
4. Added a room or scene in the Hue app? Use **Refresh rooms & scenes** in the tray menu.

## Publish a standalone .exe

Framework-dependent (smaller; needs the .NET 8 Desktop Runtime installed):

```powershell
dotnet publish HueBar/HueBar.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Self-contained (no runtime needed; larger):

```powershell
dotnet publish HueBar/HueBar.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The `HueBar.exe` lands in `HueBar\bin\Release\net8.0-windows\win-x64\publish\`. Drop a
shortcut to it in `shell:startup` to launch HueBar at login.

## Notes on the Hue API

Uses the Hue **local API v1** (plain HTTP) for MVP simplicity:

- Discovery: `GET https://discovery.meethue.com/`
- Pair: `POST http://<bridge>/api` with `{"devicetype":"huebar#windows"}` (needs link button)
- Rooms: `GET http://<bridge>/api/<key>/groups` (`type: "Room"` / `"Zone"`)
- Scenes: `GET http://<bridge>/api/<key>/scenes` (`GroupScene`s link to a room via `group`)
- Activate: `PUT http://<bridge>/api/<key>/groups/<id>/action` with `{"scene":"<id>"}`
