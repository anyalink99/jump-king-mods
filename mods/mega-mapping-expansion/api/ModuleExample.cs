using JKRuntime;
using JKRuntime.Modules;
using MegaMappingExpansion.Api;

[RuntimeModule("example.lantern", "Example Lantern", Requires = new[] { "mega.mapping.scene:1:0" })]
public static class ExampleLantern
{
    [OnLevelStart]
    public static void Start(ModuleContext context)
    {
        IMappingScene scene = context.Require<IMappingScene>("mega.mapping.scene");
        if (!scene.Available) return;
        context.Track(scene.Activate(context.ModuleId, "carried-lantern"));
    }
}
