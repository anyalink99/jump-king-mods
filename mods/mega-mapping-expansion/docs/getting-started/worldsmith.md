# Build, launch and reload your map

[Handbook](../README.md) / [Getting started](README.md)

Choose the workflow that matches your files. For lessons, use a copy of a map that already loads. The editable **source** and the **playable build** can be different folders.

## With Worldsmith Extension

1. Put the lesson's `mapping_settings.xml` and any `mme-assets` folder beside `level_settings.xml` in your source map.
2. Run `WorldsmithExtension.exe`. Choose **Open project...** and select that source folder.
3. Close Jump King, then choose **Test in-game**. Extension builds a separate test copy and starts it in the game. XML and other extra files are copied along with the native map; source images/audio are compiled as needed.
4. Check the lesson's expected result on screen 1.
5. To change it, edit the XML in the original source folder and save. Close Jump King and choose **Test in-game** again. Changed source causes the test copy to rebuild.

To inspect the final files without publishing, open **Build / Workshop**, choose **Build checked copy**, then **Open build folder**. Check that `mapping_settings.xml`, included XML and referenced assets are there. A compiled sound can be `.xnb` instead of `.wav`; textures may have compiled counterparts too.

Extension has dedicated layout/collision and **Transitions...** tools. Decorative construction scenes are authored in XML. An editor preview of ordinary map art doesn't show every MME effect; inspect the game result.

## With ordinary Worldsmith

1. Open the source map you already use in Worldsmith. Find its source folder in Explorer and locate `level_settings.xml`.
2. Add the lesson's XML and resources to that root. Save XML in your text editor.
3. Compile the level with Worldsmith's level compilation workflow. Keep the output folder Worldsmith reports; that is the playable build.
4. Open that output in Explorer. Verify it contains `mapping_settings.xml` and all included XML. Verify `mme-assets/panel.png` or its matching `panel.xnb` exists for lesson 1. Copy extra XML/resources from source if your tool omitted them, keeping the same relative paths.
5. Open the compiled level using your normal Worldsmith/Jump King local-map test workflow, with MME and Runtime loaded. Check the expected image or caption.
6. After every source edit, compile/copy again and restart the map. Saving source alone doesn't update an existing build or running scene.

Keep image paths such as `mme-assets/panel.png` in XML. MME can resolve a matching uncompressed `.xnb` when the authored image is absent. It also resolves compiled sound counterparts. Don't rename every XML reference merely because Worldsmith produced XNB files.

## Edit a playable test folder directly

This is useful if you already know how to launch a particular local map folder.

1. Copy the working playable map to a separate test directory.
2. Add the lesson's XML and assets beside that copy's `level_settings.xml`.
3. Launch that exact copy with Worldsmith Extension's **Test in-game**, or use the Steam launch-argument steps below.
4. Edit files in that copy and restart the map to apply them. No scene compiler is needed.

Avoid editing a subscribed Workshop map in place: Steam can replace it. Keep deliberate changes in an editable source project before rebuilding or distributing them.

### Launch a playable folder through Steam

1. Close Jump King. In Steam, open the game's **Properties > General**.
2. In **Launch Options**, enter `-debug "C:\Maps\My MME Test"`, replacing the quoted path with your **playable map root**. Keep the quotes when the path contains spaces. The folder must contain `level_settings.xml`, compiled native collision and the other working map resources.
3. Launch Jump King from Steam. This is the same debug-folder launch form used by Extension's level testing.
4. After testing, remove the custom folder argument to return to your ordinary launch setup. Keep `-debug` only when you want debug tools.

For edits only to MME XML and its raw PNG/WAV resources, restart this playable copy to see them. You still need your map builder when changing native collision, native art or other compiled resources.

## Reload presentation without restarting

1. Add `-debug` to Jump King's launch arguments for your local test run.
2. Load the map. Open the mod settings menu and choose **Mapping inspector**.
3. Save your XML in the **playable folder currently loaded**. An edit only in source needs a rebuild/copy first.
4. Choose **Reload scene source** in the inspector.
5. If it fails, read **Diagnostics** and `MegaMappingExpansion.log`. The previous working scene remains active.

Reload applies scene presentation and logic. Restart after changing collision, layout, screen connections or native props. Leave and reopen the map for Runtime policy changes. Reload/preview are blocked when another session owns shared world state. The [inspector guide](../reference/inspector.md) covers preview and export.

## Check a build before sharing it

Open the final playable copy and test the features your map uses. Include its XML, assets and native resources, and declare Runtime/MME plus any block-provider dependencies. A successful build checks formats and files; play the authored sequence to check its timing and placement.

[Files and packaging](files.md) lists the files to include. [Troubleshooting](../reference/troubleshooting.md) starts with verifying the exact folder the game reads.
