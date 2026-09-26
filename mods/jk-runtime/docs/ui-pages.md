# Scoped UI pages

Use `ScopedUiPage` for new page implementations and `UiPageStack` for nested
navigation. Start from `examples/UiModExample.cs`, also exported as the SDK's
editable `ModEntry.cs`. It is a complete packaged main/pause-menu example with
a numeric control, measured commands and a child text editor; no map is required.
`IUiPage` remains the low-level integration contract for specialized consumers.

| Helper | Responsibility |
| --- | --- |
| `ScopedUiPage` | Creates a RuntimeScope per open, cleans partial opens, cancels drag before closing |
| `UiPageStack` | Owns nested pages, routes only the top page, drains transition input and closes children |
| `UiPageCommand` | One action/callback/label supplies input routing and the current physical key hint |
| `UiPageLayout` | Measures commands, wraps rows, reserves title/content/status/footer inside GuiFrame |
| `UiList` / `UiListItem` | Stable-ID selection, keyboard reveal, pointer hover/click, independent wheel viewport |
| `UiNumberControl` | Finite bounds, step snapping, Left/Right, wheel and captured drag |
| `UiPointer.DragRegion` | Capture until release; cancel on focus/surface/keyboard takeover or page teardown |

## Native feedback and colors

Use `UiSounds.Play(UiSound.Move|Confirm|Change|Back|Error)` from input/action
handlers, on the game thread. `UIApi.Supports("ui-feedback-v1")` advertises this
contract. Runtime matches the installed game's policy: Move and Back
are silent; Confirm and Change use CursorMove (`selectA`); Error uses MenuFail
for a rejected operation or execution failure. The game's Select (`selectC`)
is a gameplay item-toggle cue, not a settings edit cue. Native SFX mute/master
volume still apply; missing audio cannot abort an action.

`UiList`, `UiNumberControl`, `UiPageCommand`, binding/text pages and debug actions
own their feedback. Their callbacks should not play another confirmation.
Custom pages own their own transitions. Never play from Draw or idle Update.
Disabled shared controls and numeric boundaries stay silent; captured numeric
dragging limits feedback to 16 cues/second without throttling value updates.
Runtime does not patch native selection or add native back sounds. Existing
native menus retain their own activation/edit feedback. Move/Back remain valid
semantic enum values so existing consumers inherit the corrected silent policy.
Native pointer adaptation and recovery of pre-truncation Workshop text require
the existing single shared Harmony engine; no additional engine is bundled.

Use `UiTheme.Text` (white) for normal labels, including unselected rows;
`Muted` (208 gray) for secondary descriptions and `Disabled` (128 gray) only for
unavailable controls. Gold/Cyan/Red are semantic accents, not default body copy.
Use `UiFrame` or `UiTheme.NativeFrame` for the loaded ornamental game border;
the latter reuses a frame and is game-thread only. Draw text at integer pixels.

Compact grids use three 142-pixel columns with adaptive row heights.
Each row takes the height of its tallest item; complete rows are packed into
300 pixels of content height. Short entries therefore permit more rows per page.
Only the enclosing window has a native frame; cells have no border, background
highlight or duplicated selected-title footer. Horizontal
separators are split per column; vertical separators span only the taller content
of the two neighboring entries. Segments leave gaps and never outline cells.
Selection uses the native arrow, a 16-pixel text inset and a further five-pixel
selected shift; wrapping reserves both so selection never reflows the page.
The native small font is drawn at integer pixels without scaling, with 11-pixel
line spacing. Names have four lines when metadata is present (six otherwise),
and author/status metadata has two lines. Original fitted titles and author
strings are recovered before truncation; native completion icons and their
conditional visibility are retained. Layout is cached per item and font.
Extremely long text is marked with an ellipsis within its cell; stored text and
the original detail page remain unchanged. A page counter appears only when
needed; pointer hit areas still cover the full cells.

Native settings/Workshop menus keep their items and behavior-tree structure.
Runtime repositions overflowing frames within the viewport and scrolls lists
that exceed its height. Hit areas use the visible row range. The native popup
selector's separate Draw override is adapted too. On interruption/reentry,
stale running decorators are reset; an idle selection cannot resume a former
child without Confirm. A genuinely active child retains its state. Embedded
Runtime pages close their owned lifetime when interrupted.

## Complete page

The following page is compiled as `UiPageExample.dll` by the Runtime build.
Its source is exported in the SDK as `ScopedPageExample.cs`.

```csharp
using JKRuntime.UI;
using Microsoft.Xna.Framework;

public sealed class ExampleNumberPage : ScopedUiPage
{
    private readonly UiFrame frame = new UiFrame(new Rectangle(12, 12, 456, 336));
    private readonly UiNumberControl number;
    private readonly UiPageCommand[] commands;
    private double value = 0.5;
    public ExampleNumberPage()
    {
        number = new UiNumberControl(0, 1, 0.01, () => value, v => value = v);
        commands = new[] {
            new UiPageCommand(UiAction.Confirm, "Done", () => WantsClose = true),
            new UiPageCommand(UiAction.Cancel, "Back", () => WantsClose = true)
        };
    }
    public override void Update(UiInput input, float delta)
    {
        foreach (UiPageCommand command in commands) if (command.Handle(input)) return;
        number.Update(input);
    }
    public override void Draw()
    {
        frame.Draw();
        var layout = new UiPageLayout(frame.Bounds, commands, 1);
        UiTheme.TextLine("LIGHT INTENSITY", layout.Title.Location.ToVector2(), UiTheme.Gold, false);
        number.Draw(layout.Content);
        UiTheme.WrappedText("Arrows, wheel or drag to adjust.", layout.Status, UiTheme.Muted);
        layout.DrawCommands();
    }
}
```

Host it with `UIApi.CreateMenuPage(factory, new ExampleNumberPage())` for a native
Mods page or `UIApi.Open(new ExampleNumberPage())` for an in-level modal. Acquire
page-specific registrations in `OpenPage(RuntimeScope resources)` and call
`resources.Own(lease)` / `resources.Defer(cleanup)` immediately. Keep ownership
on the page scope, not on the whole level, for resources that should end on close.

The host begins the pointer surface. Register hit regions only while drawing,
using the same rectangles used for rendering. `UiPageLayout` should be recomputed
when bindings/device, commands or frame dimensions change. Recomputing in Draw
is the simple option. Use its Content for list/slider bounds; do not position
controls against the outer frame bottom. Oversized command labels may abbreviate
within their own row; commands are not silently dropped to save a row.

`UiList.SetItems` preserves the selected ID after reorder/filter; IDs must be
nonempty and unique. `Select(id)` explicitly reveals a row. Hover changes the
selection without shifting the viewport. Wheel moves the viewport directly.
Use immutable row IDs in callbacks; never retain an index across a refresh.
Long labels fit the row; put full explanatory text in Description/status.

Numeric setters are shared by keyboard, wheel and drag. A cancelled drag restores
the value at capture; release commits. Changing page or closing while dragging
cancels before page resources are disposed. Custom pages should explicitly call
`UiPointer.CancelCapture(this)` on close. Drag callbacks remain owned by the
capture's original surface until completion; they must not retain disposed data.

All helpers are game-thread UI. Draw requires loaded native fonts/GuiFrame and an
active SpriteBatch. Layout-only `UiPageLayout(frame, footerRows, statusLines)` is
available for custom content and headless geometry checks. Too-small frames,
invalid numeric bounds and duplicate list IDs are rejected explicitly.

Text positions are snapped to whole pixels. WrappedText respects paragraph
breaks and breaks overlong words at text-element boundaries. Colors and button
borders use the shared UiTheme/native GuiFrame values. Never draw strings such
as "Secondary" as keyboard instructions: UiPageCommand resolves the actual key,
including Controls+ chords, and renders the same action used by Update.

## Shared text entry

`UiTextEntryPage(title, initial, accepted, maxLength = 64, allowEmpty = false)`
implements `IUiPage` and `IUiPageInputPolicy`. Use the same modal/embedded hosts as
other pages. The callback receives trimmed text once after successful confirmation
and button release. Cancel and forced teardown do not call it. Callback failures
stay on the page with an error and allow retry. Length must be between 1 and 1024.

Physical keyboards use Windows character messages (including the active Latin or
Cyrillic layout), independent of Controls+ menu bindings. Arrow/Home/End move the
caret, Shift selects, Backspace/Delete erase, Ctrl+A selects all, Ctrl+V pastes,
Enter confirms and Escape cancels. Control characters and surrogate code units
are excluded: this is single-line BMP text, not an IME or emoji editor.
The on-screen keyboard supports uppercase/lowercase Latin and Cyrillic, digits,
space, deletion, caret movement and confirmation using a gamepad or pointer.

Use `UiPageStack.Push(new UiTextEntryPage(...))` for composition. Do not manually
forward child lifecycle, drawing or input. The stack owns those responsibilities.

Keyboard capture uses Runtime's permanent keyboard input layer, preserving
bindings and mouse ownership. Nested captures keep blocking until all owners
close. Forced close continues suppressing held keyboard keys through release.
Runtime invalidates cached native keyboard/menu frames at capture
boundaries. Each producer owns a `JKRuntime.Input.KeyboardInputGate`, so one
fast source observing release cannot rearm a slower source with a held snapshot.
The gate observes capture transitions even if a page opens and closes between
polls. Subframe Charge 0.22 uses it for worker edges, native publication and menu
delivery. Gamepad and pointer navigation remain available to the text page.

Custom editors can acquire the same ownership with `UIApi.AcquireTextInput()`;
keep the returned lease for the entire edit and dispose it on confirmation,
cancel, parent teardown and failures. Acquisition/disposal are game-thread only.
Nested leases coexist. `UIApi.Supports("text-input-capture-v1")` discovers this
contract. The lease does not implement text commands or change bindings. MGE's
search, screen-range and name fields use it in 0.9.6.

`UIApi.IsTextInputActive` identifies the active editor. Custom shortcut/action
producers should use `KeyboardInputGate.Suppress(anyHeld)` both when sampling and
when delivering queued actions, and clear their pending queue when it returns
true. Pass the unfiltered source's held state; keep a separate gate per source,
under its existing lock if sampling and delivery use different threads. Gates
read only capture metadata and are safe on worker threads. Their release drain
outlives `IsTextInputActive`; checking that property alone is insufficient for
queued input or forced teardown. Arbitrary foreign code polling physical keyboard
state directly without this contract cannot be isolated by Runtime.
Physical input evidence samplers continue observing hardware; capture does not
rewrite their diagnostic history.

`UiTextRenderer` draws native bitmap fonts where supported and lazily allocates
one fallback atlas per instance for Latin/Cyrillic names. It is game-thread and
page-owned: dispose it on close. Rendering requires a loaded game content manager,
graphics device and active SpriteBatch. Constructors perform no GPU work.

## Nested pages

```csharp
var page = new UiPageStack(pages => new MySettingsPage(pages));
// Native menu:
var node = UIApi.CreateMenuPage(factory, page);
// Alternatively, during gameplay: UIApi.Open(page);
```

Store the supplied stack in your root page. In an Update command, call
`pages.Push(new UiTextEntryPage("Name", name, value => name = value))`.
The parent keeps its state and resources but receives no input and does not draw
until the child closes. The child sets WantsClose or uses the default Back policy;
there is no separate manual Pop/OnClose call. Back at the root closes the stack.
The factory creates a fresh root each time the whole stack reopens.

Both hosts and the stack share the same page session. They wait for keyboard,
controller and pointer buttons to release at entry and on closing; returning to
a parent rearms its input boundary. The neutral release tick is consumed too.
Pages must not simulate releases or route the same input to a hidden parent.
Navigation from Draw or child lifecycle callbacks is rejected. Do not reuse a
page instance simultaneously in multiple hosts/stacks. A stack rejects itself
and duplicate active children.

`IUiPageInputPolicy.HandlesCancel = true` means the page owns Back (for example,
to cancel an edit first). Otherwise both hosts close on Cancel. `ScopedUiPage`
retains its explicit-policy default: define a Back command setting WantsClose,
as in the canonical example, or override HandlesCancel to false. Inheriting it
does not invent navigation, layout or buttons.

Open/update/draw failures terminate that hosted lifetime and clean resources;
an embedded draw failure cannot reopen the page on the next tick. A new native
menu entry/reset permits a deliberate retry. Stack failures propagate to its
host, which closes the entire stack. Teardown attempts every child and parent
even if one cleanup throws. Failed releases remain retained for cleanup retry;
cleanup callbacks must be idempotent and must not start new navigation.
`UIApi.Supports("page-stack-v1")` discovers the new contract. No game callbacks,
workers or graphics resources are created merely by constructing a stack.

## Full-window editor cursor

`UiPointer.AcquireWindowCursor()` shares UIApi+'s native window cursor with an
in-game editor that also covers letterbox/pillarbox areas. Detect support with
`UIApi.Supports("window-cursor-v1")`. Own the returned `IDisposable` in the editor's
open scope and release it on close, failure or world teardown. Multiple leases
coexist; only the last release restores normal cursor ownership. Reset invalidates
all leases safely. Cursor movement uses the OS cursor plane, not a rendered sprite.

The lease suppresses mouse gameplay bindings and owns only cursor presentation.
The editor must manage pause, keyboard input, focus and its own full-client hit
coordinates. It does not open a modal, create a window or alter normal UI page
coordinates. Standard UIApi+ pages continue to use the existing pointer regions.

## Compact inventory

`UIApiSettings.UseCompactInventory` defaults to true and is exposed as
**Compact Inventory** in Runtime Settings. `UIApi.Supports("compact-inventory")`
reports availability. The layout adapts the installed native inventory selector;
it does not reconstruct ownership conditions or item action trees.

Three columns without headings contain registered mod items together
with Giant Boots and Snake Ring; native cosmetic equipment; and trade/miscellaneous
items including Bug's Note. Column widths follow their content. The original
inventory format supplies frame padding, anchoring, one-pixel item margins and
Back placement; no extra footer or cursor inset is added. Registered item text
retains its definition color.
Registered equipment uses its existing `IsEquipped` callback. Native equipment
uses the same enabled preference as the game's Equip toggle. Names wrap in the
native small font. The native cursor uses its 16-pixel text inset and five-pixel
selection shift. Independent column scrolling keeps all owned items reachable;
a shared native Back button remains visible. Pathological names beyond twelve
lines have a visible ellipsis. Ordinary full item names remain intact.

Only the inventory selector receives this layout/input handling. Native item
menus, stock counts and effects remain owned by their original implementation.
New visible rows rebuild the cached grouping; count changes invalidate only the
corresponding wrapped label. Unknown row types safely use the original list.
