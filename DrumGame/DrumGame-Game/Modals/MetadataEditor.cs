using System;
using System.Linq;
using System.Reflection;
using DrumGame.Game.API;
using DrumGame.Game.Beatmaps;
using DrumGame.Game.Beatmaps.Editor;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Modals;

public static class MetadataEditor
{
    static string[] fields => new string[] {
        "Title",
        "Artist",
        "Mapper",
        "Difficulty",
        "Tags",
        "RomanTitle",
        "RomanArtist"
    };


    public static RequestModal Build(Beatmap beatmap, BeatmapEditor editor, Action<RequestModal> onCommit = null, bool disableSave = false)
    {
        RequestModal request = null;

        void CommitProp(PropertyInfo prop, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) value = null;
            var oldValue = (string)prop.GetValue(beatmap);
            if (value != oldValue)
            {
                if (editor != null)
                    editor.PushChange(new MetadataChange(() => prop.SetValue(beatmap, value),
                        () => prop.SetValue(beatmap, oldValue), $"set beatmap {prop.Name} to {value}"));
                else
                    prop.SetValue(beatmap, value);
            }
        }

        var t = typeof(BJson);

        request = new RequestModal(new RequestConfig
        {
            Commands = new (Command, CommandHandlerWithContext)[] {
                (Command.SetDifficultyName, context => context.GetString(e => {
                    CommitProp(t.GetProperty("DifficultyName"), e);
                    onCommit?.Invoke(request);
                }, "Set Beatmap Difficulty Name", "Name", beatmap.DifficultyName))
            },
            Title = "Setting Beatmap Metadata",
            OnCommit = onCommit,
            DisableCommit = disableSave ? "Cannot save this beatmap" : null,
            CommitText = "Save",
            Fields = fields.Select<string, IFieldConfig>(e =>
            {
                if (e == "Difficulty") return new AutocompleteFieldConfig
                {
                    Label = e,
                    Buttons = [new CommandIconButton(Command.SetDifficultyName, FontAwesome.Solid.PenFancy, 22)],
                    Options = ["Easy", "Normal", "Hard", "Insane", "Expert", "Expert+"],
                    DefaultValue = beatmap.Difficulty.ToDifficultyString(),
                    OnCommit = v =>
                    {
                        var difficulty = BeatmapDifficultyExtensions.Parse(v);
                        var oldValue = beatmap.Difficulty;
                        if (difficulty != oldValue)
                        {
                            if (editor != null)
                                editor.PushChange(new MetadataChange(() => beatmap.Difficulty = difficulty,
                                    () => beatmap.Difficulty = oldValue, $"set beatmap Difficulty to {difficulty}"));
                            else
                                beatmap.Difficulty = difficulty;
                        }
                    }
                };
                var prop = t.GetProperty(e);
                return new StringFieldConfig(e, (string)prop.GetValue(beatmap))
                {
                    OnCommit = v => CommitProp(prop, v)
                };
            }).ToArray(),
            Footer = new Container
            {
                Children = new[] {
                    new DrumButton
                    {
                        Text = "Load From Audio File",
                        Height = 30,
                        Width = 200,
                        Action = () =>
                        {
                            var tags = AudioTagUtil.GetAudioTags(beatmap.FullAudioPath());
                            if (tags.Title != null) request.SetValue(0, tags.Title);
                            if (tags.Artist != null) request.SetValue(1, tags.Artist);
                        }
                    },
                    new DrumButton
                    {
                        Text = "Load Image From Audio",
                        Height = 30,
                        Width = 200,
                        X = 205,
                        Action = () =>
                        {
                            var ffmpeg = new FFmpegProcess();
                            ffmpeg.AddInput(beatmap.FullAudioPath());
                            // fmpeg requires file extension, even it's wrong
                            // this will still write the file as a png if it's stored in the mp3 as a png
                            var relativeImage = $"images/{beatmap.Id}.jpeg";
                            ffmpeg.ExtractImage(Util.DrumGame.MapStorage.GetFullPath(relativeImage));
                            ffmpeg.Run();
                            if (ffmpeg.Success) {
                                if (editor != null) {
                                    var old = beatmap.Image;
                                    editor.PushChange(new BeatmapChange(() => beatmap.Image = relativeImage, () => beatmap.Image = old, $"set image to {relativeImage}"));
                                } else {
                                    beatmap.Image = relativeImage;
                                }
                            }
                        }
                    },
                }
            }
        });
        return request;
    }
}