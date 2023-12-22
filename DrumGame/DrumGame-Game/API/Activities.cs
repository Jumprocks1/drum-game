using System;
using System.ComponentModel.DataAnnotations;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Utils;
using osu.Framework.Extensions.EnumExtensions;

namespace DrumGame.Game.API;

public class PlayingBeatmap : UserActivity
{
    public PlayingBeatmap(BeatmapPlayer player)
    {
        Player = player;
        Start = DateTime.UtcNow;
        // don't need to remove events since game will outlive player
        player.Track.RunningChanged += UserActivity.TriggerActivityChanged;
        player.ModeChanged += _ => UserActivity.TriggerActivityChanged();
    }
    public override DateTime? Start { get; }
    public override DateTime? End => Player.Track.IsRunning ? DateTime.UtcNow + TimeSpan.FromMilliseconds(Player.Track.RemainingTime) : null;
    public override string State =>
        Player.Mode.HasFlagFast(BeatmapPlayerMode.Playing) ? "Playing beatmap" :
        "Editing beatmap";
    public readonly BeatmapPlayer Player;
    public Beatmap Beatmap => Player.Beatmap;
    public override string Details => $"{Beatmap.Artist} - {Beatmap.Title}";
}
public enum StaticActivityType
{
    Idle, // default
    [Display(Name = "Selecting Map")]
    SelectingMap
}
public class StaticActivity : UserActivity
{
    public static StaticActivity Instance = new StaticActivity();

    StaticActivity(StaticActivityType type = StaticActivityType.Idle)
    {
        _type = type;
    }
    static StaticActivityType _type;
    public static StaticActivityType Type
    {
        get => _type; set
        {
            if (_type == value) return;
            _type = value;
            if (UserActivity.Activity == Instance)
                UserActivity.TriggerActivityChanged();
        }
    }
    public override string State => Type.DisplayName();
    public override string Details => string.Empty;
}
