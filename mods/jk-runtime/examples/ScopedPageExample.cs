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
        // Shared controls own SFX; these callbacks only change page/value state.
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
