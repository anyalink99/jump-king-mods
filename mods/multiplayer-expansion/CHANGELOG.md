# Changelog

## 0.1.0 - 2026-10-07

- Start the native Debug session with a local second client by default.
- Route UIAPI+ pointer focus and coordinates into the selected embedded client.
- Report preparation stages, gate input on readiness, cancel copying, coalesce
  repeated starts and switch transports through an orderly stop and restart.
- Stop helpers when the parent exits; bound preparation, startup and shutdown.

- Add an isolated two-client Debug lab with one host window and input selection.
- Preserve installed Multiplayer packet serialization and ghost processing over
  a real local Steam Networking Sockets connection.
- Add separate saves, compiled-map staging, packet counters and a transport probe.
- Load installed mods from isolated copies and share dependency assemblies during discovery.
- Add a Debug-only in-game entry point that embeds a second client in the existing
  game window, with Steam and direct local transport choices and a stop action.
- Cap both clients at 60 FPS and stagger startup.
- Apply the Debug draw cap before starting client 2, including after preparation errors.
- Reserve menu space for changing status text and keep detailed errors in the session log.
- Load staging utilities explicitly when the game inherits PowerShell 7 module paths.
- Include the base game's required ending metadata when preparing native Debug content.
- Route packet reads in Multiplayer's already-running receive loop when starting a native session.
- Give both copies of the Debug map the same network identity without rewriting saved attempts.
- Remove the main game's inactive sleep while client 2 is embedded and resize the
  child asynchronously. Record maximum frame gaps alongside average FPS.
- Route Runtime keyboard observations to the selected embedded client, including
  Subframe Charge's pause/menu edges, without replaying held keys on focus changes.
