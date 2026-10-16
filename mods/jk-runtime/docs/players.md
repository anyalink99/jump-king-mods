# Using JK Runtime

Install Runtime once, then enable the mods you want to use. This guide covers
the menus and controls supplied by Runtime itself. Each mod describes its own
features in its README.

## Install Runtime

Install Workshop item **3793086563** and current versions of the mods that use
it. Keep one active Runtime copy. Old binaries referencing `UIApiPlus.dll` must
be rebuilt; do not install the old UIApi+ DLL alongside Runtime.

## Menus

Navigation and Back are silent; accepted actions and settings edits use the
native select cue.

The Workshop browser uses three compact columns. Names wrap, author and status
stay visible, and the native arrow and completion icons still work. Long native
menus stay on screen and scroll when needed. If a submenu is interrupted, it
asks for confirmation again. **Optimizations** and **Diagnostic mode** are under
JK Runtime **Settings**.

**Mod compatibility fixes**, enabled by default in Settings, adapts Jump King
Manager's area buttons and screen selector to the currently loaded map. It uses
the map's area names and actual screen count, including unnamed screens. Changes
apply to open Manager windows immediately; disabling restores its original UI.
Screen navigation finds a clear arrival position from the loaded collision data.
See [compatibility](compatibility.md#jump-king-manager-loaded-map-browser)
for supported versions and limits.

The same setting adapts BoGMod3's moving-platform registration so it doesn't mark
new attempts as player-modified. Platform movement stays unchanged. This takes
effect on the next level start and doesn't erase existing attempt history.
See [moving-platform compatibility](compatibility.md#moving-platform-registration)
for the reviewed build and fallback behavior.

**Compact Inventory**, enabled by default in Settings, arranges owned items in
three unlabeled columns: mod items with cursed equipment, cosmetics, and
miscellaneous or trade items. Names wrap and each long column scrolls to its own
selection. The original check mark and inspect, equip and use actions still work
with keyboard, controller and mouse. Turning the setting off restores the native
list. Unknown rows also fall back to that list so their actions are not lost.
See [UI validation](ui-validation.md) for coverage, costs and remaining limits.

## Mod Inspector

Open **JK Runtime -> ModsDebugActions -> Mod Inspector** for loaded blocks,
factory provenance, materials, mod state and experimental scoped overrides.
It replaces MGE's Gimmick Library and stores its own configurations and searches.
Browsing doesn't construct material samples;
use **Prepare material sample**, Preview or Apply explicitly.
See [Mod Inspector](mod-inspector.md) for controls and supported behavior.

## Controls+

Controls+ edits primary/secondary bindings, including two-button chords.
Mods can expose their own **Binds** page with only their registered actions,
using the same primary/secondary slots, Clear and Default commands.
Keyboard profiles support physical left/right, middle, X1 and X2 mouse buttons;
movement and wheel scrolling are not held-button bindings. Mouse bindings have
no defaults. Open a binding slot, then press and release the desired buttons.

## Pinned settings and toggle shortcuts

Open **JK Runtime > Pinned settings**, select a mod, then select a setting to
pin or unpin it below Resume in the pause menu. Toggle settings can also be made
bindable with the displayed Boots command. Assign their shortcuts in Controls+.
The shortcut still respects the original setting's availability and save rules.
A successful toggle plays the native equipment cue; rejected or unchanged values
stay silent.

## Mouse controls

The first left click in a menu only reveals the cursor. After that, move to
select, click to activate, scroll to browse and right-click to go back. Keyboard
or controller input hides the cursor; Escape goes back without leaving mouse
mode. Native sliders and options accept clicks on their left and right halves.
The cursor ignores letterbox bars. Supported windows use the Windows cursor
plane, with a software cursor as a fallback. Menu actions still run on the game
thread.

## Prompted jump power in Debug Mode

Hold the right mouse button over the game and drag upward or downward to set
`P:` above the king. Release to keep the prompt. Drag again to adjust it, or
right-click without moving to turn it off when the button is released.
The number uses Jump% hold frames. When Subframe Charge's **Enable Substep Charge**
is active, each logical pixel selects one **Charge Step**: a half, quarter or
eighth frame, or a 1 ms hold step. Map-authored intervals take precedence over
the player's selection. Otherwise four pixels select one frame. The range
follows the current surface's charge multiplier.

Tap Jump to charge and execute the selected power. Each charge starts neutral;
Left or Right held at its start or tapped during it latches that direction for
this jump. Movement before charging doesn't count. The arrow appears only during
the charge and clears on takeoff or cancellation. The prompt uses the game's
MenuFont and normal jump effects. Pausing, losing focus,
teleporting or switching controllers cancels a pending charge. The prompt is
available only in the game's `-debug` mode and is never saved.

## Settings and run flags

Settings live beside the DLL in `JKRuntime.Settings.xml`. Legacy UIApi+ settings
are migrated once, retaining the original and a backup. Mouse chords remain in
Runtime settings rather than being exported as native controller button codes.

At results, `Modified by:` lists known contributors to the native modified-player
flag. It cannot identify unknown contributors, clear the flag or change
achievement eligibility. Treat the list as registration history rather than
proof that a feature was used: an enabled mod may register handlers at startup
without any player input. If the game resets a save while carrying a counter
forward, Runtime keeps the witnessed sources. Removed handlers do not carry into
the next attempt.

Text-entry pages share a keyboard editor and an on-screen Latin/Cyrillic keyboard.
Typing is isolated from menu bindings; held keys stay blocked through close.

## Collect a report

Open **JK Runtime > Runtime diagnostics** to export a text/JSON report beside
the DLL. For stutters, enable **Diagnostic mode**, reproduce the problem, then
turn it off. Reports stay on your computer.

Read the [diagnostic workflow](diagnostic-workflow.md) for capture files and
startup tracing. Include the map, mod versions and steps to reproduce when
sharing a report. The [troubleshooting guide](troubleshooting.md) helps locate
loading, activation and state errors.
