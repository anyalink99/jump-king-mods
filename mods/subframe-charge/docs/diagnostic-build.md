# Automatic SFC jump diagnostics (sfc-evidence-v1)

Subframe Charge 0.25.0 includes this recorder in the normal mod and requires
JK Runtime 1.30 or newer. It retains evidence around `SFC: Not supported` without
changing charge rules. No special DLL, diagnostic setting or restart is needed
to enable recording when SFC correction or Show SFC is active. With both off,
the charge observer remains detached as before.

## Report a problem

1. Play with the same map, controls and mod settings that
   produced the problem. After `Not supported`, keep playing for another second,
   then close the game normally. A few occurrences are sufficient.
2. Send `SubframeCharge.log`, plus `.1` and `.2` if present, from the
   mod folder (`steamapps/workshop/content/1061090/3794767640` for Workshop).
   Include the approximate time, map name, keyboard/controller
   model and whether the jump felt wrong or only the label changed. Send all
   files together, not just lines containing the message.
3. The log header contains `evidenceTrace=sfc-evidence-v1` and the mod version.
   Older temporary builds wrote `SubframeCharge.Diagnostic.log`; those files are
   preserved but are not the destination for new records.

## What the evidence means

The recorder retains up to 768 recent records in memory. A failed launch writes
that history and the next 30 player frames. Closely spaced failures share the
post-history window and each keeps its reason and charge ID. Ordinary SFC log
messages go into the diagnostic log too. Three files of up to 16 MiB each are
retained; collect them soon after reproduction. File writing is asynchronous.

Records include monotonic QPC timestamps and their frequency, physical samples
of the configured Jump keys only, worker history/consumer cursors, sampler source
readiness, raw native pad Jump fields, SFC's observed Jump value, native timer and
`_can_jump`, input latches, frame/charge IDs, position/screen, focus/overlay state,
restore epoch and reset/resume/clear events. Physical sample timestamps are
separate from the later time SFC drains an edge. `deliveryMs` alone is not a
worker stall. `physical-history-overflow` means the diagnostic reader missed
history; it does not itself invalidate gameplay input.

Assembly versions/MVIDs, body behaviours and Harmony patches to input/jump/body
methods are recorded at attachment and again at the first incident. This is
evidence of installed code, not proof that a listed mod caused the problem.
Only the shared Harmony engine's registry is visible. No general keyboard text
is recorded or uploaded. Keyboard evidence combines physical keyboards.

Diagnostics inspect cached fields and physical history without consuming input,
adding input subscriptions, polling devices or installing new gameplay hooks.
Private layouts are optional: missing data is labelled unavailable/busy and
diagnostic errors must not stop gameplay. Native getter return values inside
foreign patches are not intercepted. Gamepad evidence uses native pad fields
and sampler readiness; independent physical key history covers keyboard/mouse.
Additional recording has overhead; `maxCaptureUs` reports the largest individual
capture cost (including warmup), not the total per-frame cost. This build helps
separate timing/order/lifecycle failures; it cannot prove an OS scheduling cause.

## Build and validation

Run `scripts/check-mods.ps1 -Mod subframe-charge -Integration` to build against
the current workspace Runtime. The normal output is
`build/subframe-charge/UPLOAD_TO_WORKSHOP/SubframeCharge.dll`.
For a deliberately pinned SDK, `build.ps1 -RuntimeAssembly <path>` uses that DLL
for compilation, tests and the package's minimum API requirement.
The recorder fixture runs in normal builds and checks bounded before/after history, overlapping failures,
and preservation of native jump eligibility and Runtime consumer queues.
