# HueBar — notes for agents

Small Windows tray app for Philips Hue. Three projects:

- `HueBar.Core` (`net8.0`) — all testable logic: Hue API client, models, settings,
  room/scene mapper, pairing coordinator. **No** WinForms/WPF dependency.
- `HueBar` (`net8.0-windows`) — WinForms tray shell + WPF settings pane. UI glue only.
- `HueBar.Tests` (`net8.0`) — xUnit suite over `HueBar.Core`.

## Testing policy — read before changing logic

This repo has an enforced testing policy: see [`TESTING.md`](TESTING.md). In short:

- All significant logic must be covered by tests, and the tests ship **in the same
  change** as the code.
- Prefer TDD; a red test must fail because the behavior is wrong, not because something
  doesn't compile yet. No hollow/"bullshit" tests.
- Testable logic belongs in `HueBar.Core` so it runs headlessly. If you find yourself
  wanting to test something inside the WinForms/WPF layer, extract it into Core first.

`dotnet test HueBar.sln` is a required status check on `main`; PRs cannot merge red.
