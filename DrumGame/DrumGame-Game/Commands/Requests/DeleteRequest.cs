using System;
using DrumGame.Game.Modals;
using osu.Framework.Graphics;

namespace DrumGame.Game.Commands.Requests;

public enum DeleteOption
{
    Delete,
    DeleteMapAndAudio
}
public class DeleteRequest : RequestModal
{
    public DeleteRequest(string map, Action<DeleteOption> onSelect) : base(new RequestConfig { Title = $"Do you want to delete the map {map}?", CloseText = null })
    {
        Add(
            new ButtonArray(i =>
            {
                if (i != 2) onSelect((DeleteOption)i);
                CloseAction();
            }, new ButtonOption
            {
                Text = "Delete Map"
            }, new ButtonOption
            {
                Text = "Delete Map and Audio",
                AutoSize = true
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
