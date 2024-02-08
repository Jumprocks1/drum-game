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

    public static RequestModal DtxSaveRequest(string currentName, Action<bool> convert)
    {
        var req = new RequestModal(new RequestConfig
        {
            Title = "DTX files cannot be saved directly.",
            Description = "Would you like to convert to a BJson file before saving?",
            CloseText = null,
            Field = new BoolFieldConfig($"Remove {currentName} after converting", true)
            {
                Tooltip = "This is recommended to prevent duplicate maps appearing in the selector."
            }
        });
        req.Add(new ButtonArray(i =>
            {
                req.Close();
                if (i == 0) convert(req.GetValue<bool>(0));
            }, new ButtonOption
            {
                Text = "Convert"
            }, new ButtonOption
            {
                Text = "Cancel"
            })
        {
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre,
            // bit sketchy. Field isn't generated until RequestModal is added to parent, so we need a constant
            Y = BoolFieldConfig.Height + 10
        });
        return req;
    }
}
