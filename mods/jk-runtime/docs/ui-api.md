# JK Runtime UI API

Part of Runtime SDK **1.30**. `UIApi.Supports("compact-inventory")` reports the optional native inventory layout.
Start with the [handbook](index.md) or the
[complete page example](ui-pages.md). All live UI runs on the game thread.
Menu factories can run during startup even if their page is never opened; keep
constructors/getters lightweight. Load map files in `BeforeAttempt`, and acquire
page-owned resources when the page opens. See [preparation](preparation.md).

## Native feedback and palette

Use `UiSounds` and `UiTheme` for shared native feedback and colors. The
[page guide](ui-pages.md#native-feedback-and-colors) owns the sound, palette,
layout and grid contracts, including which control owns a feedback cue.

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

## Complete pages with measured layout

The [page guide](ui-pages.md) owns complete page construction and lifecycle
examples using `ScopedUiPage`, `UiPageLayout`, `UiPageCommand`, `UiList` and
`UiNumberControl`. Use [nested pages](ui-pages.md#nested-pages) for `UiPageStack`
ownership. This API reference covers the individual services and registrations.

## Scrollable lists

Keep a `UiListViewport` per list/page. In Draw, call
`FirstVisible(count, visibleRows, selected)`; this reads/clamps the stored viewport
without following hover changes. Hover callbacks only update selection.
After explicit keyboard/controller navigation, call
`FollowSelection(selected, count, visibleRows)`. For wheel input, set selection to
`Scroll(-delta, count, visibleRows, selected)`; positive row steps move down.
This retains selection's screen row and clamps at both ends. Reset when opening
a new list; retain the instance when returning to a previous page.

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

Reference `JKRuntime.dll`. The public entry point is `JKRuntime.UI.UIApi`. This
subsystem provides menus, bindings and world interactions. Query optional contracts with
`UIApi.Supports("capability")`; current capabilities include
`registration-scopes`, `world-interactions-v1`, `merchant-trading`,
`inventory-items-v1`, `inventory-equipment-v1`, `compact-grid-pages-v1`, `binding-chords`,
`physical-binding-resolution`,
`ui-feedback-v1`, `vanilla-ui-theme`, `compact-workshop-grids`, `pinned-mod-settings`,
`mod-setting-labels`, `mod-toggle-bindings`, `embedded-menu-pages`,
`transparent-modal-pages`, `secondary-modal-action`, `main-menu-control`,
`main-menu-return`, `root-main-menu-items`, `root-pause-menu-items` and
`pause-menu-control`, `menu-items-v2`, `menu-action-feedback` and
`modal-input-policy`, `action-button-hints` and `workshop-menu-items`.

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
instead of silently pretending the change was saved.

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
