# More Items API 1.0

Reference the SDK implementation `MoreItems.Module.dll` and required `JKRuntime.dll`.
Compile your own runtime module and package it through JK Runtime's SDK; do not
ship loose implementation DLLs. The native Workshop shell is `MoreItems.dll`.
The single public entry point is `MoreItems.MoreItemsApi`.

## Items

Register an item during `BeforeLevelLoad`:

```csharp
MoreItemsApi.Register(new ConsumableDefinition(
    "my-mod.token",
    "Token",
    "A persistent custom currency.",
    null,
    null,
    new Color(244, 194, 58),
    "Tokens"));
```

`ConsumableDefinition` is the historical name of the item-definition value
object; it is not a separate subsystem or dependency. More Items owns its
registry, persistence, Inventory integration and world objects.

Stacks are permanent and shared by Vanilla and every custom world. Changing
worlds or restarting a run never changes the inventory. Starting a new run
clears only that world's collected pickup IDs, so placed pickups respawn
without deleting anything already owned.

A non-null use callback adds the normal action beneath Inspect. Persistent
equipment uses `isEquipped` and `setEquipped`; JK Runtime UI then creates Jump King's
ordinary Equip row with its standard checkbox. A `ConsumableHotkey` registers
the matching Controls+ binding and gameplay action. `FromChords` accepts
one- or two-button defaults.

## Item modules

An independently switchable item family registers an `ItemModuleDefinition`
through the same `MoreItemsApi` entry point:

```csharp
MoreItemsApi.RegisterModule(new ItemModuleDefinition(
    "my-mod.token",
    "Token",
    GetEnabled,
    SetEnabled,
    delegate(MenuSelector page)
    {
        page.AddChild(new TokenColorOption());
    },
    InstallRuntime,
    UninstallRuntime));
```

The registry owns runtime lifecycle. It installs enabled modules when a level
starts, uninstalls them in reverse order, and refreshes one module when its root
toggle changes. Generic More Items runtime contains no item-specific
special cases.

Use `MoreItemsApi.CreateModuleToggle(id)` for a native root setting. Enabled
modules with item-specific settings appear dynamically in `Items Settings`;
their root enable switches are not duplicated inside the cards.

## Merchant and world objects

`MoreItemsApi.RegisterCurrency` exposes an item stack as merchant currency.
`RegisterMerchantOffer` publishes an offer to the More Items Bargainburg
catalog. JK Runtime UI still owns the generic merchant UI and transaction contract.

`SpawnPickup`, `SpawnLoosePickup` and `SpawnDispenser` create item world
objects. Loose pickups expire and are capped. Dispensers are appearance-free:
map art remains in background, midground or foreground while code owns only
the interaction point and ejection physics.

Custom levels may use `props/more-items/items.xml`; the root is
`<MoreItems version="1">`. Pickup and dispenser IDs share one namespace,
screens are one-based and coordinates are screen-local. See
[examples/items.xml](../examples/items.xml).

### Map-authorized equipment

In the map's `level_settings.xml`, append the case-sensitive `AllowHammer` tag
to its existing `Tags` list:

```xml
<Tags>
  <string>AllowHammer</string>
</Tags>
```

Like `AllowJetpack` and `AllowRewinder`, this controls the item's contribution to
native modified-run accounting, not item availability. Missing tags, a missing
level, or tags for other items do not authorize Hammer. Hammer reads the current
level on each controller installation, including persistent equip across worlds.
Without authorization, equipping marks the run immediately since it replaces
ordinary movement. With authorization, Hammer registers through Runtime's
unmarked body pipeline. Neither path clears earlier or foreign run markers.

More Items registers its development commands through
`UIApi.RegisterDebugAction`. `ModsDebugActions` belongs to JK Runtime UI while every
action remains owned by its source mod.

Runtime supports live debug values through the extended
`UiDebugActionDefinition` constructor: `getLabel`, `adjust(direction)` and
`confirmLabel` accompany the usual execute callback. Hammer uses this for
force changes and reset; the existing plain-action constructor is unchanged.
