using JKRuntime;
using JKRuntime.Modules;
using JKRuntime.UI;

// compile to an implementation DLL, then pass it to sdk/package.ps1
// only the generated discovery shell belongs in UPLOAD_TO_WORKSHOP
[RuntimeModule("example.runtime", "Runtime Example")]
public static class RuntimeExample
{
    [OnLevelStart]
    public static void Install(ModuleContext context)
    {
        var ui = context.Require<UiServices>("jk.ui");
        var registrations = context.Track(ui.CreateScope(context.ModuleId));
        registrations.RegisterDebugAction(new UiDebugActionDefinition(
            "example.runtime.report", "Runtime Example", "Export runtime report",
            delegate { RuntimeApi.ExportDiagnostics(); }));
    }
}
