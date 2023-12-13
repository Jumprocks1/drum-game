using System;
using DrumGame.Game.Modals;
using osu.Framework.Allocation;
using osu.Framework.Graphics;

namespace DrumGame.Game.Commands.Requests;

public class ConfirmationModal : RequestModal
{
    readonly Action OnConfirm;
    public ConfirmationModal(Action onConfirm, string title, string description = null) : base(title, description)
    {
        OnConfirm = onConfirm;
    }

    public string YesText = "Yes";
    public string NoText = "No";

    [BackgroundDependencyLoader]
    private void load()
    {
        Add(
            new ButtonArray(i =>
            {
                if (i == 0) OnConfirm();
                CloseAction();
            }, new ButtonOption
            {
                Text = YesText
            }, new ButtonOption
            {
                Text = NoText
            })
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            }
        );
    }
}
