# Temporary frame probe

Build with `build.ps1`. This standalone diagnostic mod instruments the installed
game's update/draw and entity/component dispatch methods without changing input,
state, simulation cadence or saves. It is not a Runtime release dependency.

Close the game before installing only `JKFrameProbe.dll` in `Content/JKMods`.
Disable the separate StartupTrace marker and remove other temporary probe DLLs
first. Load a map, play normally and visit the pause menu for at least 45 seconds.
The first two seconds are omitted. A level exit finishes a shorter capture.
Two more 45-second windows can automatically follow without restarting an attempt.
Closing the game finishes and writes a partial capture before process exit.
Each attempt writes uniquely named text/CSV files beside the DLL on a worker.
Storage is bounded to 32768 frame records and 2048 runtime types per attempt.

Reports include frame gaps, update/draw durations, thread allocation counts when
supported by the CLR, GC counts and aggregated component/entity timings. Nested
timings overlap. Instrumentation has overhead; comparisons require the same
probe configuration. Background work and display latency are not attributed to
an entity. Menu/paused/unfocused samples are separate from gameplay samples.

After collecting the capture, close the game, remove the probe DLL and retain
its logs. Do not leave it installed for normal play. The build checks native
hook installation, unchanged dispatch counts and detached capture ownership.
