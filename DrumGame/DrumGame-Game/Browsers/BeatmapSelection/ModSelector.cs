using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Modals;
using DrumGame.Game.Modifiers;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;

namespace DrumGame.Game.Browsers.BeatmapSelection;

public class ModSelector : RequestModal
{
    readonly BeatmapSelectorState State;

    static float ButtonMargin => CommandPalette.Margin;

    const int Columns = 5;

    public ModSelector(BeatmapSelectorState state) : base(new RequestConfig
    {
        Title = "Selecting Mods",
        Width = (ModButton.Size + ButtonMargin) * Columns - ButtonMargin
    })
    {
        State = state;
    }

    CommandButton ClearButton;

    [BackgroundDependencyLoader]
    private void load()
    {
        var mods = BeatmapModifier.Modifiers;
        var row = 0;
        var col = 0;
        foreach (var mod in mods)
        {
            Add(new ModButton(State, mod)
            {
                X = (ModButton.Size + ButtonMargin) * col,
                Y = (ModButton.Size + ButtonMargin) * row
            });
            col += 1;
            if (col >= Columns)
            {
                col = 0;
                row += 1;
            }
        }
        AddFooterButton(ClearButton = new CommandButton(Command.ClearMods)
        {
            Text = "Clear Mods",
            Width = ModButton.Size,
            Height = 30
        });
        State.OnModifiersChange += UpdateClearButton;
        UpdateClearButton();
        Util.CommandController.RegisterHandlers(this);
        Util.CommandController.AddPriority(Command.ToggleMod);
        Util.CommandController.AddPriority(Command.ClearMods);
    }

    void UpdateClearButton() => ClearButton.Enabled.Value = State.HasModifiers;

    protected override void Dispose(bool isDisposing)
    {
        Util.CommandController.RemoveHandlers(this);
        Util.CommandController.ReducePriority(Command.ToggleMod);
        Util.CommandController.ReducePriority(Command.ClearMods);
        State.OnModifiersChange -= UpdateClearButton;
        base.Dispose(isDisposing);
    }

    [CommandHandler] public void ClearMods() => State.ClearModifiers();
}