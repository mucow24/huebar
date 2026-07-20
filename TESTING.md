# Testing Policy

HueBar is small, but the logic that talks to the bridge and persists your credentials
is exactly the kind of thing that breaks silently. This policy keeps that logic honest.

## The rules

1. **All significant logic must be covered by unit and/or integration tests.**
   "Significant" = anything with a branch, a transformation, a parse, or an I/O contract:
   the API client, the room/scene mapper, settings persistence, the pairing loop. Pure UI
   glue (drawing, layout, event wiring) is exempt — see [What we don't test](#what-we-dont-test-and-why).

2. **Tests accompany their code change.** A commit or PR that adds or changes significant
   logic includes the tests for it in the *same* change. "I'll add tests later" is how
   later never comes. CI enforces this by running the suite on every PR.

3. **Use TDD as much as reasonably possible.** For new logic, write the failing test
   first, watch it fail for the right reason, then make it pass. For pre-existing
   untested code, add characterization tests that pin the current *intended* behavior.

4. **No bullshit tests.** A red test must fail *because the behavior under test is wrong*,
   not because a type doesn't exist yet or a module failed to load. "The class I haven't
   written won't compile ⇒ red" is **not** a real red — it proves nothing about the
   feature. Before trusting any red, confirm it is asserting the actual bug or feature and
   would go green only when that behavior is correct. Likewise, don't assert on
   implementation trivia (private field names, call counts that don't matter) — assert on
   observable behavior.

## Where tests live

All logic worth testing lives in **`HueBar.Core`** (a plain `net8.0` library with no
WinForms/WPF dependency), so the whole suite runs headlessly on any OS.

- Test project: [`HueBar.Tests`](HueBar.Tests) — [xUnit](https://xunit.net/).
- Run locally: `dotnet test HueBar.sln`
- The suite is a **required status check** on `main` (see [`.github/workflows/ci.yml`](.github/workflows/ci.yml)).
  A PR cannot merge until it passes.

## What we test

| Area | Type | What's covered |
|------|------|----------------|
| `RoomSceneMapper` | unit | Room/Zone filtering, scene→group attachment, sorting, `includeZones` |
| `HueClient` | integration* | Every HTTP method via a fake `HttpMessageHandler`: happy paths, `HueApiException` on error arrays, link-button (type 101), network failure, malformed bodies |
| `AppSettings` | unit | JSON round-trip, `IsConnected` truth table, corrupt-file recovery, load/save against an isolated directory |
| `BridgePairing` | unit | The retry-until-link-pressed / timeout / bridge-error loop, with an injected clock so it runs instantly |
| `PairResult` / models | unit | Factory methods, `LinkButtonNotPressed`, `[JsonPropertyName]` wiring against real bridge JSON shapes |

\* "integration" here = the client exercised end-to-end against a stubbed transport, not
the happy-path-only "does it return non-null" variety.

## What we don't test, and why

Being explicit here *is* part of "no bullshit tests" — we don't write hollow tests just to
raise a coverage number on code where the test would assert nothing meaningful:

- **`IconFactory`** — runtime GDI+ drawing. There is no behavioral contract to assert
  beyond "it produced an icon"; a pixel-diff test would be brittle and prove little.
- **`TrayApplicationContext`, `SettingsForm`, `SettingsView` (WinForms/WPF)** — UI shells
  and event wiring. The *logic* they used to contain (the pairing loop) has been extracted
  into `BridgePairing` in Core and is tested there. What remains is presentation: menu
  construction, `ElementHost` sizing, status-string mapping.
- **`Program.Main`** — process entry point / argument dispatch.

If logic worth testing grows inside any of these, extract it into `HueBar.Core` and test
it there, rather than reaching into the UI to test it in place.
