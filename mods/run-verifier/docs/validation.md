# Run Verifier validation

Run from the repository root with Jump King installed:

```powershell
.\scripts\check-mods.ps1 -Mod run-verifier,replays -Integration
```

The selected checks build Runtime, Run Verifier and Replays, then exercise their
native contracts and isolated graphics fixtures. They do not complete a live run
or validate a deployed website.

## Mod coverage

- Completion capture, session boundaries, telemetry, area timing, modifier
  evidence, missing intervals and environment changes.
- Native clock comparison using the game's actual `TimeSpan`. The installed
  .NET Framework game rounds `TimeSpan.FromSeconds(1f / 60f)` to 17 ms; comparing
  it to an ideal 1/60 second would incorrectly reject ordinary attempts.
- Profile persistence, atomic checkpoints, recovery, pending upload jobs and
  replay associations after asynchronous saves.
- Native menu open/confirm/release/back behavior, scoped pages, device hints and
  pointer regions on completion-marker selection.
- Built-in and Workshop completion markers without invented times or dates;
  unfinished attempts remain unverified.
- Circular-seal encoding, error correction and native results-screen rendering.

## Website contract

The website is maintained separately. Before a coordinated release, verify the
[protocol](protocol.md) against its server and screenshot decoder:

- Profile ownership, one-time sign-in codes, expiry, CSRF and protected routes.
- Observation order, native timing, signed report integrity and evidence levels.
- Replay permissions, staged uploads, retry/deduplication and data recovery.
- C#/TypeScript seal parity, PNG/JPEG decoding, scaling and documented crop limits.
- Unlisted/public visibility, moderation, deletion and map identity merges.

## In-game checks

Complete a run offline and online, then check history, the native seal and replay
attachments. Exercise interrupted sessions, reconnects, pending-upload retries
and a map change. Confirm that playback cannot create a completion.

A signed seal authenticates an accepted report. It does not authenticate arbitrary
screenshot pixels or prove an unmodified client. Moderated screenshots and imported
completion markers remain unverified. See [verification limits](../README.md#verification-limits).
