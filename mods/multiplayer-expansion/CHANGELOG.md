# Changelog

## 0.8.0 - 2026-10-10

- Draw remote players through Runtime's world layer under camera composition,
  bypassing the original wall-triggered frame flag and late duplicate draws.
- Include Smooth Camera's presentation state in the two-client Debug status.

- Put per-player Teleport to buttons directly below Multiplayer settings.
- Use Runtime topology for remote placement, seamless rider transitions and
  side-aware proximity arrows. Keep original rectangular king contacts.
- Keep proximity directions on the default region map instead of letting a
  distant cyclic route move a right-hand branch to the left.
- Read the installed Multiplayer player list as a field and use its public
  ghost interface for indicators, preventing a draw-time null reference.
- Share one keyboard snapshot across native controls and UI consumers per
  two-client simulation step, avoiding repeated full OS scans on the active client.
- Reuse installed ghost interfaces and tracker metadata during rendering. Avoid
  duplicate incoming world validation once replica authority is established.
- Poll test-session stop files at 5 Hz and skip switch-key OS calls while the
  two-client session is inactive.
- Record separate update and draw maxima in the Debug performance log so long
  frame gaps aren't hidden by average FPS.
- Project proximity arrows from canonical coordinates once, avoiding repeated
  portal rebasing while Smooth Camera pans.
- Require JK Runtime 2.0 and use its shared world registry, authority and actors.
- Remove Expansion's duplicate native world, foreign state and serializer code.
  Keep the public byte-registration API as a forwarder for existing integrations.
- Use Runtime's shared bird/wind and reviewed Switch Blocks services. Include
  declared Mapping scene state, hidden walls and authored native side links.
- Preserve existing movement, contact and equipment protocols.

## 0.7.0 - 2026-10-09

- Split the UIAPI+ window into Host Settings, Binds and Teleport, with nested
  Debug and network tools. Keep navigation and binding capture in Runtime.
- Add a rebindable Kick action, defaulting to K on keyboard. The host validates
  range, terrain, cooldown and participant state before confirming additive
  momentum. Negotiate action support, deduplicate events and invalidate delayed
  requests when the host changes action permissions.
- Draw an expanding pixel shockwave and confirmed impact burst. Use the native
  bump handler for the target and stage impulses before its next body step.
- Add a same-map teleport player list with terrain-safe, overlap-free placement.
  Add separate saved host permissions for kicks and teleports.
- Preserve keyboard, mouse and controller binding codes through Runtime device
  layers. Route existing and new mouse layers to the embedded window and restore
  their original readers when testing stops. Gate native device buttons and
  physical keyboard sampling across client selection, focus and Steam Overlay.
- Move carrying and contacts into owned Runtime body phases. Ground riders before
  gravity and materials so Snake Ring uses native ice friction. Keep terrain
  landings authoritative over delayed passenger-clearance checks.
- Expose head planes through generic read-only foot geometry, without changing
  No Walk Off or inserting peers into map collision. Preserve established walking
  sides during near-contact rendering and keep airborne passengers unanchored.
- Cover native material/landing order, carrier/rider jumps, scope cleanup, sound,
  foot queries, action validation, replay, impulse lifetimes and placement in
  focused regression checks.

## 0.6.1 - 2026-10-09

- Package the main mod as a dependency-free native discovery shell. Load the
  implementation and world callbacks together through JK Runtime so Workshop
  load order can't hide the Multiplayer Expansion entry.
- Preserve installed settings and helper paths when Runtime loads the embedded
  assembly. Expose separate main and pause menu factories and retire the old
  standalone world shell.
- Use a thin client bootstrap to run the same Runtime-owned implementation in
  the second Debug client. Keep native menu discovery separate from the helper
  and route Runtime input before the game starts.
- Check discovery in a fresh process before Runtime and Multiplayer are loaded,
  then check SDK loading, initialization and the installed data directory.
- Apply client-copy retention to Workshop's default session directory and custom
  directories as well as the development `sessions` directory. Measure native
  world preparation and mod-provider discovery separately through Runtime.

## 0.6.0 - 2026-10-09

- Add a checked Workshop release package and a native-sprite cover. Verify
  matching helper/world versions and keep the 256 x 256 preview below 35,000 bytes.
- Render remote movement on a buffered, monotonic snapshot clock with bounded
  cubic interpolation, same-clock pose/support and gradual contact alignment.
  Keep collision prediction separate; stop speculative visual movement during
  packet gaps, sample each peer once per draw frame and match native sprite pixel
  alignment for bodies and equipment.
- Add an independent World synchronization setting, negotiated host snapshots,
  bounded provider data and ordered, deduplicated world input requests.
- Read the network map identity once per native update and refresh it on world
  and attempt preparation, avoiding repeated legacy save reads for every entity.
- Share the host wind cycle and native raven motion; let remote players trigger
  the host's raven and Switch Blocks behaviors. Keep the Switch Blocks adapter
  inside the expansion and protect guests' local block-state saves.
- Validate legacy Multiplayer packets, drain/coalesce queued movement, limit
  paused ghost trackers and interpolate in world coordinates. Guard failed or
  stale lobby callbacks, refresh the owner, and announce idle players on join.
- Stop copying current and rotated mod logs into test clients. Apply the two-copy
  retention limit to completed rapid restarts while protecting active processes,
  incomplete preparation, saves, settings, logs and rollback backups.

## 0.5.2 - 2026-10-09

- Keep Ghosts and Solid modes, with a separate Push other players toggle. Solid
  always includes head platforms and body collisions. Migrate older partial rules
  to Solid and remember pushing across mode changes.
- Make Multiplayer's existing Ghost opacity control remember one value per mode.
  Preserve the original Ghosts opacity and start Solid at 100%; both remain editable.
- Recognize Runtime's layered keyboard by its stable profile ID so dedicated
  controls reach the unselected client. Cover the real Runtime wrapper in tests.

## 0.5.1 - 2026-10-09

- Add dedicated movement for the unselected client in two-client tests: [ left,
  ] right and \ jump, alongside the selected client's normal controls. Switching
  input swaps their target. Preserve native charge/release,
  block menu actions and require release after focus or menu changes.
- Reserve the three keys during testing without changing saved bindings.

## 0.5.0 - 2026-10-08

- Add protocol 4 with explicit playing, paused, spawning, teleporting and unavailable
  states, simulation ticks and state revisions. Require 0.5.0 for physical peers;
  older peers retain legacy visibility.
- Preserve collision event numbering and consumed IDs across menus and rule changes.
  Queue received impacts and validate them at the next native body step.
- Add bounded visual correction with shared support-chain rendering and exact local
  contact positions. Separate transport, event staging and rendering responsibilities.
- Add Debug delay, jitter, loss and duplicate controls, bounded packet recording and
  background replay verification. Defaults preserve the clean local connection.
- Test four seeded simulated peers through jumps, pause, teleport and reconnection.
  Fix fractional corrective movement crossing terrain boundaries; reduce temporary
  contact and event allocations and include network counters in session diagnostics.

## 0.4.3 - 2026-10-08

- Add number-row 6 to switch input in native two-client mode, with one switch per
  press, foreground checks and input filtering for both games and UIAPI+.
- Choose contact ownership before overlap recovery. Airborne kings resolve their
  contact against grounded kings without moving the grounded player.
- Keep live paused players visible and solid with stationary network snapshots.
  Share presence rules between outgoing state and the support graph, clear old
  impacts on pause transitions, and reset the ghost draw guard every render frame.

## 0.4.2 - 2026-10-08

- Make Push other players off also prevent indirect walking pushes from side
  correction and overlap recovery. Preserve the established contact edge when
  predicted or delayed walking snapshots cross it; keep spawn recovery and bounces.
- Add sustained two-client contact checks with immediate and delayed snapshots.

## 0.4.1 - 2026-10-08

- Play the native bump sound for player bounces, underside hits and passenger
  ceiling impacts, with underwater selection and no repeated sound on confirmation.
- Separate spawn, teleport and lingering body overlaps using a bounded search
  that checks terrain and other players. Keep movement available without repeated
  impulses when there is no reachable free space.
- Discard old pending impacts on a detected teleport. Preserve head carrying and
  pass-through behavior in Ghosts and one-way Platforms modes.

## 0.4.0 - 2026-10-08

- Replicate support relationships so a passenger follows the local carrier without
  waiting for remote coordinates. Carry passengers through jumps and let them
  jump again in midair with inherited movement; check their ceiling clearance.
- Add native-coefficient wall bounces and shared airborne collision impulses.
  Confirm pair impacts through numbered, repeated records with prediction
  reconciliation and protection against duplicates, old attempts and newer jumps.
- Predict current contacts using measured packet delay and observed acceleration.
  Reject retired attempt packets and stop redundant legacy sends among updated peers.
- Replace the expansion's FPS limiter with a temporary Subframe Charge performance
  suspension. Require Subframe Charge 0.27.2+ if that mod is installed; preserve
  saved settings and restore them when two-client mode ends.

## 0.3.0 - 2026-10-08

- Send timestamped body, pose, facing and native equipment snapshots at up to 60 Hz.
- Use one bounded interpolation history for remote rendering and physical contact,
  with short prediction, sequence checks and reset on teleport, pause or map change.
- Bypass the original ghost replay queue and mixed screen/world interpolation for
  expansion peers. Drain Debug packet bursts and keep the newest legacy state.
- Draw the sender's equipped native layers without changing the receiver's items.
  Custom Wardrobe+ artwork and presentation packs aren't transferred.
- Add regressions for jitter, loss, pause, teleport, shared moving support, equipment
  encoding and installed ghost hooks.

## 0.2.0 - 2026-10-08

- Add a UIAPI+ settings window with Ghosts, Platforms and Solid presets, plus
  separate head-platform, body-collision and pushing settings.
- Let the lobby host publish rules; require matching acknowledged rules and
  recent body snapshots before applying contact.
- Add terrain-checked head landings, moving-player support, side/underside
  collisions and bounded local pushing. Release stale, paused and other-map peers.
- Keep extension packets separate from Multiplayer's ghost parser in online
  lobbies and both Debug transports.
- Move two-client tools into their own page. Keep those tools Debug-only.
- Require JK Runtime 1.44.1 or newer for the shared UI window.

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
