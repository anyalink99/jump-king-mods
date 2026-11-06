# Require MME for your map

[Map guides](README.md)

If your map depends on MME collision or logic, tell players they need the mod.
Add **Mega Mapping Expansion** and **JK Runtime** as required items on your
map's Steam Workshop page.

You can also make Runtime check for MME when the map loads. This gives players
a message if the required mod is missing or too old.

## 1. Add the requirement file

In your map folder, create a folder named `jk-runtime`. Inside it, create
`policy.xml`:

```xml-policy
<MapPolicy version="1">
  <Module id="mega-mapping-expansion" mode="require" minimum="1.3.0" reason="This map needs MME for its collision and scenes" />
</MapPolicy>
```

Keep the module ID and `mode="require"` as shown. `minimum` is the oldest MME
version your map supports. Change `reason` to a short explanation of what your
map uses; players see it when there's a problem.

## 2. Include it and check the message

Build the map and check the built folder contains `jk-runtime/policy.xml`.
Leave and reopen the map so Runtime reads the file again.

With both mods enabled, the map should open normally. To check the error,
disable MME, reopen the map and read the message. Enable MME again afterwards.

Runtime must be installed to read this file, so keep the Workshop requirements
too. The file doesn't subscribe to or download mods for players.

Requiring MME doesn't turn on its optional scenery checkbox. If your map needs
scene logic to finish, tell players to enable it and test what happens when
it's disabled.

For conflicts with other mods, mechanic restrictions and cooperative suspension,
see [all policy rules](../reference/map-policies.md).
