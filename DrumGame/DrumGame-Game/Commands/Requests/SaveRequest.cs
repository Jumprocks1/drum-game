using System;
using DrumGame.Game.Modals;
using osu.Framework.Graphics;

namespace DrumGame.Game.Commands.Requests;

public enum SaveOption
{
    Save,
    DontSave
}
public class SaveRequest : RequestModal
{
    public SaveRequest(Action<SaveOption> onSelect) : base(new RequestConfig
    {
        Title = "Do you want to save your changes?",
        Description = "Your changes will be lost if you don't save them.",
        CloseText = null
    })
    {
        Add(
            new ButtonArray(i =>
            {
                CloseAction();
                if (i != 2) onSelect((SaveOption)i);
            }, new ButtonOption
            {
                Text = "Save"
            }, new ButtonOption
            {
                Text = "Don't Save"
            }, new ButtonOption
            {
                Text = "Cancel"
            })
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }
        );
    }
}
