using JKRuntime.Modules;
using JKRuntime.UI;
using Microsoft.Xna.Framework;

// Canonical standalone SDK entry point: no map assets or gameplay hooks required.
[RuntimeModule("example.ui-page", "Example Page")]
public static class ModEntry
{
    [MainMenuItemSetting]
    public static JumpKing.PauseMenu.BT.TextButton MainPage(object factory, JumpKing.PauseMenu.GuiFormat format)
    {
        return new JumpKing.PauseMenu.BT.TextButton("Settings",
            UIApi.CreateMenuPage(factory, new UiPageStack(pages => new ExampleSettingsPage(pages))));
    }
    [PauseMenuItemSetting]
    public static JumpKing.PauseMenu.BT.TextButton PausePage(object factory, JumpKing.PauseMenu.GuiFormat format)
    { return MainPage(factory, format); }
}

internal sealed class ExampleSettingsPage : ScopedUiPage
{
    private readonly UiFrame frame = new UiFrame(new Rectangle(12, 12, 456, 336));
    private readonly UiNumberControl number;
    private readonly UiPageCommand[] commands;
    private double intensity = 0.5;
    private string name = "Example";
    internal ExampleSettingsPage(UiPageStack pages)
    {
        number = new UiNumberControl(0, 1, 0.05, () => intensity, value => intensity = value);
        commands = new[] {
            new UiPageCommand(UiAction.Confirm, "Rename", () => pages.Push(
                new UiTextEntryPage("Name", name, value => name = value))),
            new UiPageCommand(UiAction.Cancel, "Back", () => WantsClose = true)
        };
    }
    public override void Update(UiInput input, float delta)
    {
        foreach (var command in commands) if (command.Handle(input)) return;
        number.Update(input);
    }
    public override void Draw()
    {
        frame.Draw();
        var layout = new UiPageLayout(frame.Bounds, commands, 1);
        UiTheme.TextLine("EXAMPLE SETTINGS", layout.Title.Location.ToVector2(), UiTheme.Gold, false);
        number.Draw(layout.Content);
        UiTheme.WrappedText(name, layout.Status, UiTheme.Muted);
        layout.DrawCommands();
    }
}
