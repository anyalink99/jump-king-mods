using System;
using MegaMappingExpansion.Api;

// This consumer is compiled against MegaMappingApi.dll only, then executed in focused checks.
public static class SceneExample
{
    public static void Verify(IMappingScene scene)
    {
        if (!scene.Available) throw new InvalidOperationException("Load a scene before applying effects");
        using (var effect = scene.Apply("example.mod", new EffectDefinition {
            Id = "dim-panel", Duration = 30,
            Changes = new[] { new SceneChange { Target = "panel", Property = "opacity", Value = "0.25" } }
        }))
        {
            if (!effect.Active || effect.RemainingSeconds != 30) throw new Exception("Effect handle contract failed");
            if (scene.DescribeProperties("panel").Length == 0) throw new Exception("Property metadata missing");
            var panel = Array.Find(scene.InspectObjects(), o => o.Id == "panel");
            if (panel.Values[Array.IndexOf(panel.Properties, "opacity")] != "0.25") throw new Exception("External effect did not update the effective property");
        }
        if (scene.InspectEffects().Length != 0) throw new Exception("Effect ownership leaked");
    }
}
