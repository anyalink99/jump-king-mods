# Completion verification integration

[Run Verifier](../../run-verifier/README.md) can link saved replays to completion
reports. Players don't need it to record, watch or race a ghost. This page is for
mods consuming the completion bridge; [recording and playback](recording.md)
describes the file format and viewer ownership.

## Bridge API 1

For integration, `Replays.VerificationBridge` API 1 exposes `IsPlayback`,
`BindRun(runId + ":" + sessionId)`, `Watch(id)` and
`Saved(binding, replayId, path, startSeconds, endSeconds)`. Bind on the game thread
after recording starts. `Saved` runs on the writer thread after an atomic save;
handlers must not access game objects. Manual snapshots retain their capture
association. Restarted recordings need a new binding. Positional recordings are
not a deterministic simulation or a cheat verdict.
