# JK Runtime UI API

Part of Runtime SDK **2.0**. Use this reference when you know which UI service
you need. For a first page, follow [UI pages](ui-pages.md) or build the
[SDK example](getting-started.md).

All live UI runs on the game thread. Menu factories can run before gameplay
activation, so keep constructors and getters cheap. Prepare map files before
play and acquire page resources when the page opens. See
[preparation](preparation.md).

## Registration lifetime

Stable IDs make ordinary `Register...` calls idempotent. Every registry also
has a matching `Unregister...` operation. Level-owned registrations should use
`UiRegistrationScope`; disposing it removes only registrations still owned by
that scope, so a newer replacement with the same ID is not accidentally
removed.

```csharp
// Fragment inside OnLevelStart(ModuleContext context):
var scope = context.Track(new UiRegistrationScope("my-mod.level"));
scope.RegisterInteraction(WorldInteraction.Action(
    "my-mod.switch", "Use", 100, IsNearSwitch, ToggleSwitch));
// Context cleanup also covers a partially failed activation.
```

## Mod settings

JK Runtime UI discovers native `PauseMenuItemSetting` methods without requiring a
second integration API. Players can enable compact four-direction Workshop grids
for levels, skin collections, individual skins and mods,
pin any discovered setting directly below Resume and explicitly make any
discovered `IToggle` bindable. Bindable toggles appear in Controls+ with an
empty hotkey and keep the setting's own `CanChange` and persistence behaviour.

Mods can inspect the same catalog or pin an entry programmatically:

```csharp
foreach (UiModSettingInfo setting in UIApi.GetModSettings())
{
    if (setting.ModName == "My Mod" && setting.Label == "Enabled")
    {
        UIApi.SetModSettingPinned(setting.Id, true);
        if (setting.IsToggle)
            UIApi.SetModSettingBindable(setting.Id, true);
    }
}
```

The fallback display label is inferred from the option type. A mod may provide
an explicit label by stable setting ID before or after discovery. A scoped
registration restores the inferred label when the scope is disposed:

```csharp
scope.RegisterModSettingLabel(
    "MyMod.MyMod.Settings.ShowTrails",
    "Show trails");
```

## Complete pages with measured layout

The [page guide](ui-pages.md) owns complete page construction and lifecycle
examples using `ScopedUiPage`, `UiPageLayout`, `UiPageCommand`, `UiList` and
`UiNumberControl`. Use [nested pages](ui-pages.md#nested-pages) for `UiPageStack`
ownership. This API reference covers the individual services and registrations.

## Native feedback and palette

Use `UiSounds` and `UiTheme` for shared native feedback and colors. The
[page guide](ui-pages.md#native-feedback-and-colors) owns the sound, palette,
layout and grid contracts, including which control owns a feedback cue.

## Scrollable lists

Keep a `UiListViewport` per list/page. In Draw, call
`FirstVisible(count, visibleRows, selected)`; this reads/clamps the stored viewport
without following hover changes. Hover callbacks only update selection.
After explicit keyboard/controller navigation, call
`FollowSelection(selected, count, visibleRows)`. For wheel input, set selection to
`Scroll(-delta, count, visibleRows, selected)`; positive row steps move down.
This retains selection's screen row and clamps at both ends. Reset when opening
a new list; retain the instance when returning to a previous page.

Draw `UiTheme.ScrollBar(track, count, first, visibleRows)` beside custom rows.
This shared proportional indicator is available since API 1.44 (`list-scrollbar-v1`). It
draws nothing for an empty or short list and clamps its thumb inside the track.
It is a position indicator, not a draggable control. Keep its track outside row
hit targets. `UiList` already draws it; don't add another one to that component.

See [UI ownership and layout](ui-pages.md#choosing-shared-ui-components) before
adding another page-specific viewport, command handler or footer calculation.

## Mouse navigation

Query `UIApi.Supports("pointer-navigation-v1")`. Call `UiPointer.BeginSurface(this)`
at the start of a custom page's `Draw`. Modal and embedded hosts do this
automatically; a page can redeclare its surface to block all underlying regions.
Register `Region(bounds, hover, click)`, `ActionRegion(bounds, UiAction.Confirm,
hover)` and `ScrollRegion(bounds, delta => ...)` as visible elements are drawn.
Callbacks run during the next game update, never during rendering. Positive
wheel delta means scroll up; clamp selections to the current list size.
`UiInputHints.Command` registers a clickable command automatically in a command
bar; legacy `new UiCommand` labels are display-only.

Only the last drawn surface receives pointer input. Capture pages should use
`BeginSurface(this, false)` so mouse buttons remain available for rebinding.
The first left click only reveals the cursor. Right click sends Cancel. Every
physical keyboard key except Escape hides the cursor, including unbound keys;
controller input also hides it. Escape alone preserves mouse mode when navigating
back. `Position` is in integer 480x360 game pixels.
Native menu integration requires the game's one already loaded Harmony engine.

Reference `JKRuntime.dll` and enter through `JKRuntime.UI.UIApi`. This subsystem
owns menus, bindings and world interactions. Check an optional contract with
`UIApi.Supports("capability")` before using it. Current capabilities include:

- ownership and interaction: `registration-scopes`, `world-interactions-v1`;
- inventory and trading: `merchant-trading`, `inventory-items-v1`,
  `inventory-equipment-v1`;
- layouts and theme: `compact-grid-pages-v1`, `ui-feedback-v1`,
  `vanilla-ui-theme`, `compact-workshop-grids`;
- bindings and input: `binding-chords`, `physical-binding-resolution`,
  `modal-input-policy`, `action-button-hints`;
- mod settings: `pinned-mod-settings`, `mod-setting-labels`,
  `mod-toggle-bindings`, `embedded-menu-pages`;
- modal pages: `transparent-modal-pages`, `secondary-modal-action`;
- root menu control: `main-menu-control`, `main-menu-return`,
  `root-main-menu-items`, `root-pause-menu-items`, `pause-menu-control`;
- menu items: `menu-items-v2`, `menu-action-feedback`, `workshop-menu-items`.

## Input and bindings

Register a gameplay hotkey with `UIApi.RegisterInputAction`. JK Runtime UI selects one
highest-priority registered action for each new press. A contextual interaction
wins over a registered action using the same press. Modal pages and key capture
own input until they close, preventing rebinding from navigating the underlying
menu and preventing Escape from both closing a merchant and opening Pause.

Controls use `UiBindingDefinition`. JK Runtime UI also discovers the common mod
settings pattern containing a `KeyBindings` dictionary. Discovery is a
compatibility layer: explicit registrations win by stable ID, two bindings with
the same visible label are retained, and missing persistence hooks are reported
as persistence errors.

Controls+ is built into JK Runtime. It is always available and automatically
discovers compatible native mod bindings in addition to explicit registrations.

A chord is one or two simultaneously held buttons represented by `UiChord`.
Chord-aware rows capture the first press, allow a second press while the first
is held, and save only after every captured button is released. Register a
chord-aware binding and matching action like this:

```csharp
UIApi.RegisterBinding(new UiBindingDefinition(
    "my-mod.open-panel",
    "My Mod",
    "Open panel",
    GetPanelChords,
    SetPanelChords,
    ResetPanelChords));

UIApi.RegisterInputAction(UiInputActionDefinition.FromChords(
    "my-mod.open-panel",
    "Open panel",
    100,
    GetPanelChords,
    CanOpenPanel,
    OpenPanel));
```

When more than one registered action becomes eligible on the same press, the
most specific matching chord wins; priority breaks ties of equal chord length.
Non-winning actions remain consumed until their buttons are released.

The legacy `int[]` constructors remain supported and retain their original
meaning: every integer is an alternative single-button binding. Controls+
upgrades Jump King's own rows and automatically discovered `KeyBindings` rows
through internal virtual buttons, so those rows can still execute a two-button
chord without changing the old mod's data contract. Their persisted settings
retain usable physical single-button values; the chord definition belongs to
JK Runtime UI.

Mods that need to sample a runtime binding outside Jump King's normal frame
input can call `UIApi.ResolvePhysicalBinding(pad, runtimeButtons)`. The returned
jagged array is OR between rows and AND within a row: each row is one physical
single-button or chord alternative. Query
`UIApi.Supports("physical-binding-resolution")` before using this optional
contract across versions.

## Focused binding pages

`UiBindingsPage` is the reusable primary/secondary editor used by Smooth Camera,
MGE and Ball King. Query `UIApi.Supports("binding-pages-v1")` for optional use.
Pass explicit registered binding IDs, in the desired order. It never expands an
empty or unavailable selection to all mods. With multiple IDs, Up/Down or the
header arrows change actions; Left/Right selects a slot. Mouse selects either
keycap. Rebind captures one button or a two-button chord, Clear removes the chosen
alternative, and Default invokes the provider's reset callback for this action.

```csharp
// Fragment inside a mod's menu factory, after registering its binding elsewhere.
return new JumpKing.PauseMenu.BT.TextButton("Binds", UIApi.CreateMenuPage(factory,
    new UiBindingsPage("Binds", "Press Dash while airborne.", "my-mod.dash")));
```

The [compiled UI example](../examples/ExampleIntegration.cs) includes main and pause
factories. The page implements `IUiPage` and `IUiPageInputPolicy`, so it also works
with `UIApi.Open`. All lifecycle, drawing and edits run on the game thread.
Set `ShowList = true` to show the selected actions together as Controls+-style
rows, without the description and single-action navigation header. This layout
requires Runtime 1.37.1 (`binding-list-page-v1`); the default layout is unchanged.
Constructing it only copies IDs; it performs no provider discovery, file IO,
preference reads or background work. Register providers before opening. Missing
providers stay visibly unavailable; replacing a registration resolves the new
provider without rebuilding the page. A provider change during capture cancels it.

Capture waits for the opening buttons to release, collects physical buttons,
and waits three native updates after full release. Focus loss, disconnection,
a device object/profile change or closing the page discards unfinished input.
During capture, menu commands and pointer navigation are blocked so their buttons
can become bindings, including mouse buttons supplied by Runtime's keyboard pad.
To cancel an unfinished capture, leave/focus out of the page. Cancellation writes
nothing. Normal Back closes the page when capture is inactive.

Both this editor and Controls+ use the same `UiBindingDefinition` and slot update
logic. They preserve other alternatives and deduplicate equivalent chords.
Alternatives are compacted: clearing Primary moves Secondary into Primary.
Single-button definitions capture one button. Registration owns storage, defaults,
per-device policy and gameplay activation; the page does not invent another
settings file, change a feature toggle, or migrate IDs. In particular, existing
`auto.*` adapters retain their Runtime device profiles and virtual chord handling.
`SetChords` and `Reset` may throw: the page reports a save error and stays open.
Providers must make their persistence transactional; the UI cannot undo arbitrary
side effects from a failing callback. Getters run only for the visible page and
must remain cheap. OnClose releases capture and pointer ownership, not registrations.

## Optional binding modes

Runtime 1.42 displays Mode before Primary and Secondary in Controls+ and both
`UiBindingsPage` layouts. `UiBindingDefinition.Mode` describes supported choices.
Unknown bindings show a locked **Unknown** cell; known one-shot actions show a
locked **Press** cell. Neither is converted by guessing from a label or key name.

```csharp
binding.Mode = new UiBindingModeOption(
    () => settings.ActivationMode,
    value => SaveActivationMode(value));
```

The default constructor offers Hold and Press, with Hold as the default. Supply
the four-argument overload with an explicit list to offer Both; Smooth Camera
implements it for its three manual views. `AvailableModes` returns a copy,
`CanChange` identifies editable options, and the `(mode, reason)` constructor
creates a fixed mode. Confirm/click cycles allowed choices. Default on Mode
restores only the mode; Default on a key slot restores the binding.

For explicitly registered options, the provider owns storage and gameplay
behavior. The UI doesn't turn an arbitrary `Execute` callback into a reversible
action. Actions registered through `UiInputActionDefinition` default to fixed
Press unless their provider supplies a mode contract. Draft definitions can keep
mode changes in their page's Apply/Cancel transaction.

For an automatically discovered enum binding, annotate its member with
`[UiBindingMode(UiBindingMode.Press)]` to declare a fixed mode while retaining
the existing binding adapter and per-device profiles. Ball King's Morph uses
this contract. Directly registered actions such as Air Dash set `Mode` on their
binding definition.

Runtime converts the native Left, Right and Jump holds to toggles in Press mode.
Menu navigation keeps physical input and displays Press; native key repeat
doesn't turn a menu step into a toggle. Boots/ring can use Hold for temporary
wearing; release, pause or focus loss restores the previous equipment unless
another action has already changed it. Bindable native mod-setting toggles expose
Hold/Press through their `IToggle` contract. Mode choices live in Runtime settings;
transient latches aren't saved and require release after interruptions/rebinding.
Subframe Charge uses native logical charging for toggled Jump, because a physical
key release no longer means that charging ended.

Automatic conversion uses per-action context guards. Preparation analyzes supported
foreign prefixes on native Jump/Walk and equipment entry points. A bounded
forward analysis identifies block predicates whose inactive paths retain native
control. During play, Runtime reads covered boolean backing fields rather than
invoking foreign getters or replaying callbacks. Dormant mechanics don't disable
conversion. An active replacement returns only the affected action to native
input and clears its latch; a check after material processing covers the first
landing before the native input/behavior tree runs. Walking keeps its current
logical input for body-stage consumers such as No Walk Off.

Comparison-only physics rewrites can retain conversion when preparation verifies
that input reads, native instructions and branch destinations are preserved.
This inspects the transpiled instructions and bounded scalar helpers; gameplay
callbacks aren't executed for the check. Installing a gravity mod alone isn't
evidence of an active input mechanic.
The activation barrier finalizes targets changed by native startup callbacks;
later patch changes invalidate coverage until the next attempt.

Uncovered rewrites/controllers suspend conversion only for their associated actions. Predicate,
helper and native-method patch changes invalidate prepared coverage. Custom player
ownership suspends conversion while that controller owns the character. Controls
keep the saved mode editable and show the fallback reason below the list. Fresh
input re-arms conversion afterward. External equipment writes relinquish a temporary
hold's ownership, including writes of the same value. Neither map names nor a
blanket foreign-block rule decide availability.

Providers can set `UnavailableReason` to a callback returning a reason when their
mode selector must be locked. `Available` and `CanChange` reflect that guard.
Use `FallbackReason` for a temporary gameplay fallback that should leave the
preference editable. `Reason` exposes either explanation.
The provider must implement its gameplay fallback too; a UI guard alone doesn't
change input. Provider-owned modes such as Smooth Camera's retain their own map
permission checks. Arbitrary runtime-generated mechanics or foreign code that
doesn't expose its ownership cannot be universally certified by this system.

Automatic foreign discovery inspects managed IL and the live controller-patch
graph without executing gameplay callbacks. It currently recognizes a controller
postfix that assigns a direct physical-key predicate to a private boolean and
whose external consumers only gate float arithmetic. That proves a held-state
path and enables Hold/Press conversion. A recognized previous/current edge
pipeline is fixed Press. Other branches, escaped state, mixed consumers, missing
metadata or unsupported patterns remain Unknown. These bounded patterns aren't
a universal proof of arbitrary mod behavior; reflection, runtime code generation
and later foreign patch changes require their own contracts. No mod-name whitelist
or speculative press/release replay is used. `binding-mode-conversion-v1` exposes
this facility; `binding-modes-v1` remains the selector contract.

## Dynamic native rows

`NativeMenuRows.After(anchor, readRows)` adds sibling buttons immediately after
a native `TextButton`, including a mod's settings button. Return one
`NativeMenuRow(id, label, action)` per available entry, or an empty collection to
show nothing. Keep IDs stable: labels and actions refresh while selection stays
on the same button. The rows inherit the anchor's font and native menu layout;
unsupported glyphs use its fallback and labels fit within 416 logical pixels.
Removed running children use Runtime's deferred menu mutation path. The anchor
owns the registration through a weak table; no process-wide player list remains.
Recheck availability in the action because a peer may leave after the last refresh.

## Modal pages

An `IUiPage` receives one resolved `UiAction` per update. `UIApi.Open` suspends
every enabled player component, consumes entry and exit presses and draws the page in the foreground.
Modal input is gated at the active device rather than rewriting saved bindings;
newly connected devices join the same context. Player components and devices
are restored even when third-party `OnOpen`,
`Update`, `Draw` or `OnClose` code throws.

The player's Boots binding is exposed to a modal page as
`UiAction.Secondary` and `UiInput.Secondary`. JK Runtime UI owns that press while the
page is open, so overlays can provide a second command without triggering the
underlying inventory action.

`UIApi.Open` may be called while another UIApi page is open. Pages form a modal
stack: only the top page receives input and each page is drawn above the previous
one. Cancel closes one layer by default. A page with nested internal navigation
implements `IUiPageInputPolicy` and returns `HandlesCancel = true`; it can then
use Cancel to step back and set `WantsClose` only at its own root.
`UIApi.ModalDepth` reports the current depth.

Custom pages can use `UiFrame`, which wraps Jump King's native `GuiFrame`, and
the public `UiTheme.TextLine`, `Tab`, `WrappedText`, `Keycap` and `CommandBar`
helpers. A frame is created once and reused by the page, so ordinary drawing
does not allocate a new nine-slice every frame. The compile-checked example
contains a complete custom `IUiPage` and registers it as a world interaction.

### Button hints

Use `UiTheme.CommandBar` / `Keycap` for control hints, with a physical button
and a short description of its current action. Do not show internal action names
such as "Secondary" or hardcode a keyboard key for a rebindable action.
Resolve labels during `Draw`, so rebinding and switching the active controller
are reflected immediately:

```csharp
UiTheme.CommandBar(UiTheme.FooterRow(frame.Bounds),
    UiInputHints.Command(UiAction.Confirm, "Select"),
    UiInputHints.Command(UiAction.Secondary, "Favorite"),
    UiInputHints.Command(UiAction.Cancel, "Back"));
```

`UiInputHints.Key(action)` returns one valid binding on the active device.
An overload accepts an explicit `PadInstance`. Confirm uses the menu Confirm
binding with Jump as an alternative; Cancel uses Cancel with Pause as an
alternative; Secondary uses Boots. Controls+ chords expand to their physical
buttons. An unavailable or unbound action shows `-`, never an invented default.
Describe Secondary by what it does on this screen, such as Favorite, Flip or Erase.
Keep enough room for the key and action label, including longer controller names.

### Pixel layout and palette

Use `UiTheme.Border` for neutral borders: it matches native GuiFrame's #727272.
Reserve Gold/Cyan for selection or emphasis. `CommandBar` uses content-sized groups
with a 16 px gap and vertically centered 20 px badges; it does not distribute
commands into equal columns. Put related commands on separate rows when needed
(for example, seeking above playback controls), leaving at least 4 px between rows.
Avoid overcrowding a row: constrained text is fitted, never drawn over its neighbor.

Place footer commands with `UiTheme.FooterRow(frame.Bounds)`.
This reserves 16 px inside the owning frame on its bottom and sides. Pass `1`
for the row above it; adjacent rows have a 4 px gap. Reserve the area above the
upper row for descriptions and status messages instead of relying on screen Y
coordinates. Undersized frames are rejected. This works with `GuiFrame.GetBounds()`
and ordinary panel rectangles as well as `UiFrame.Bounds`.

`TextLine` snaps the final text origin to whole game pixels. For other native fonts,
use `UiTheme.DrawText(font, text, position, color)`, including centered captions.
Do not add a half-pixel correction or change the shared SpriteBatch transform.
The game already draws its 480x360 target with PointClamp. Snap only UI text;
this contract does not change camera, character or gameplay positions.

Native menu nodes that distinguish Jump and Confirm can use
`UiInputHints.Command(JKpadButtons.Jump, "Make bindable")` and the equivalent
`Key(JKpadButtons)` overload. `UiAction` overloads match modal page routing.

Independent mods can place the same `IUiPage` directly inside Jump King's main
or pause menu without reflection:

```csharp
return new TextButton(
    "My page",
    UIApi.CreateMenuPage(factory, new MyPage()));
```

Embedded pages receive Cancel like every other resolved action and decide
whether it moves within the page or sets `WantsClose`. This permits detail and
confirmation states without stacking extra menu nodes.

World overlays may keep the level fully visible while
retaining modal input ownership and player suspension:

```csharp
UIApi.Open(new MyOverlay(), new UiModalOptions(false));
```

An embedded page that starts an in-world overlay may call
`UIApi.ClosePauseMenu()` after queuing the overlay. The request closes only the
pause layer; main-menu pages are unaffected.

An embedded main-menu page may call `UIApi.ContinueFromMainMenu(factory)` to
start the currently loaded world after it has queued its work. The factory is
the same object supplied to the mod's `MainMenuItemSetting` method.

Full-screen playback can call `UIApi.ReturnToMainMenu()` to request Jump
King's normal level teardown and return to the title screen. Mods that need a
first-class title-screen entry can register it at the stable location before
Extras:

```csharp
UIApi.RegisterMainMenuItem(UiMainMenuItemDefinition.Page(
    "my-mod.library",
    "My Library",
    UiMainMenuPlacement.BeforeExtras,
    100,
    context => new MyLibraryPage(context.ContinueGame)));
```

The priority provides deterministic ordering when several mods target the same
anchor. The registration is idempotent by ID and is also available through
`UiRegistrationScope.RegisterMainMenuItem`. When at least one registered item
is present, the root menu keeps its vanilla lower edge and grows upward.

Use `UiMainMenuPlacement.Workshop` (Runtime 1.7) to put a page inside the native
Workshop submenu, before Back, instead of at the root. This placement is never
injected into Pause. It uses the same registration, priority and lifetime API.
Native `MainMenuItemSetting` / `PauseMenuItemSetting` methods are separate:
they expose entries inside Mods and do not register standalone root rows.

Mods can also add a first-class command to Jump King's root pause menu without
knowing its private behavior-tree layout:

```csharp
UIApi.RegisterPauseMenuItem(UiPauseMenuItemDefinition.Action(
    "my-mod.save-snapshot",
    "Save snapshot",
    UiPauseMenuPlacement.BeforeSaveAndExit,
    100,
    SaveSnapshot));
```

An action that should acknowledge success without opening another page can
temporarily replace its own label. JK Runtime UI owns the timer and restores the
ordinary label automatically:

```csharp
UIApi.RegisterPauseMenuItem(UiPauseMenuItemDefinition.FeedbackAction(
    "my-mod.save-snapshot",
    "Save snapshot",
    UiPauseMenuPlacement.BeforeSaveAndExit,
    100,
    delegate
    {
        return SaveSnapshot()
            ? UiMenuActionResult.Completed("Saved!", 1f)
            : UiMenuActionResult.Rejected();
    }));
```

`UIApi.CreateFeedbackButton` provides the same contract for a vanilla mod
settings section or another integration point that directly expects a
`TextButton`.

The anchor is resolved from the localized vanilla `Save & Exit` row. The
registration is idempotent by stable ID, supports
`UiRegistrationScope.RegisterPauseMenuItem`, and participates in the same
factory refresh as pinned settings. The high-level factories keep Jump King's
behavior-tree and menu-factory objects inside JK Runtime UI. The original raw-node
constructors remain available for unusual native integrations. No calling-mod
IDs or special cases are stored in JK Runtime UI.

## World interactions

Use `WorldInteraction.Action` for immediate operations and
`WorldInteraction.Page` for an `IUiPage`. For known screens, prefer
`ScreenAction` and `ScreenPage`; those registrations are indexed by screen and
are not evaluated elsewhere.

Map-authored points live in `props/ui-api/interactions.xml`. Load them with
`WorldInteractionMap.LoadLevel()` in `BeforeAttempt`, then register the prepared
points at activation using `CreateAction` or `CreatePage`. The complete
[UI integration example](../examples/ExampleIntegration.cs) owns these lifetimes.
Coordinates are local to the declared one-based screen. JK Runtime UI
checks the active camera screen before testing the player's local hitbox, so
the same coordinates are safe on vertical and teleported planar maps.

The XML root accepts `version="1"`. IDs must be unique and rectangles must fit
inside `480x360`. See [examples/interactions.xml](../examples/interactions.xml).
Art remains ordinary map background, midground, foreground or native prop data;
the API does not prescribe a door, NPC or dispenser sprite.

## Merchant interfaces

Register currencies with `UiCurrencyDefinition`, offers with
`MerchantOfferDefinition`, and a complete merchant with `MerchantDefinition`.
The shared page supports custom icons, dynamic prices, ownership predicates,
availability predicates and explicit exchanges. Failed grants and exchanges
attempt to roll spent currency back and report the failing provider.

JK Runtime UI contains no merchant catalog or world-specific NPC adapter. The mod that
owns a merchant supplies inventory operations, sprites, availability and offers.

## Inventory and Equip integration

Item mods can add rows to Jump King's normal Inventory without reflecting over
its private menu tree. Register an item during `BeforeLevelLoad`:

```csharp
UIApi.RegisterInventoryItem(UiInventoryItemDefinition.Equipment(
    "my-mod.jetpack",
    "Jetpack",
    "A custom piece of equipment.",
    new Color(244, 194, 58),
    GetOwnedCount,
    IsEquipped,
    SetEquipped,
    delegate { return GetOwnedCount() > 0; },
    CanEquip));
```

JK Runtime UI generates the row, count, Inspect page and Jump King's standard Equip
toggle with its normal checkbox. The owner remains responsible for persistence,
availability and Equip semantics.
Call `UIApi.NotifyInventoryItemChanged(id)` after ownership or equipped state
changes. Registrations are idempotent and can also be owned by a
`UiRegistrationScope`.

Independent mods can reuse the same compact three-column text grid as the compact Workshop
browser without copying its navigation or Escape layering:

```csharp
IBTnode grid = UIApi.CreateCompactGrid(factory, new[]
{
    new UiCompactGridItemDefinition("Jetpack", jetpackSettings),
    new UiCompactGridItemDefinition(
        "Rewinder",
        rewinderSettings,
        null,
        delegate { return RewindersEnabled; })
});
```

The optional visibility predicate is evaluated while the grid is active, so a
card can appear or disappear immediately when its owning feature is toggled.

## Debug actions

`ModsDebugActions` is JK Runtime UI's generic host. Gameplay mods register their own
commands with `UiDebugActionDefinition`; JK Runtime UI stores no knowledge of the
items or systems those commands manipulate.

The complete external example is [ExampleIntegration.cs](../examples/ExampleIntegration.cs).
`verify-api.ps1` compiles it against the produced DLL so documentation drift is
caught before release.
