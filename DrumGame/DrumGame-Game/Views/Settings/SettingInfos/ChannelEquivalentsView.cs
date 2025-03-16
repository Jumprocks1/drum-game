using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Modals;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Views.Settings.SettingInfos;


public class ChannelEquivalent
{
    public DrumChannel InputChannel;
    public DrumChannel OutputChannel;
    public ChannelEquivalent((DrumChannel Input, DrumChannel Map) e)
    {
        InputChannel = e.Input;
        OutputChannel = e.Map;
    }
}

public class ChannelEquivalentsView : RequestModal
{
    static BindableChannelEquivalents EquivalentsBindable => Util.ConfigManager.ChannelEquivalents;
    static ChannelEquivalents Equivalents => EquivalentsBindable.Value;
    TableView<ChannelEquivalent> Table;
    DrumChannel? Channel;
    public ChannelEquivalentsView() : this(null) { }
    public ChannelEquivalentsView(DrumChannel? channel) : base(new RequestConfig
    {
        Title = channel == null ? "Editing Channel Equivalents" : $"Editing Channel Equivalents ({channel})",
        Width = 700f
    })
    {
        Channel = channel;

        var descriptionText = "Channel equivalents should be used when your kit does not have every possible drum/cymbal/channel.";
        descriptionText += "\nHitting an input channel will count for notes that are set to that input channel,";
        descriptionText += "\nor any of the equivalent channels configured here.";
        var description = new MarkupText(descriptionText);
        Add(description);


        Add(Table = new TableView<ChannelEquivalent>(new()
        {
            RowHighlight = DrumColors.RowHighlight.MultiplyAlpha(0.5f),
            CellHighlight = DrumColors.BrightGreen.MultiplyAlpha(0.4f),
            CellPadding = new MarginPadding(2),
            ExtraCellTooltip = (column, row) =>
            {
                var channel = row.InputChannel;
                if (channel == DrumChannel.None) return null;
                var midiNotes = Util.ConfigManager.MidiMapping.Value;
                var found = 0;
                var o = "<brightGreen>MIDI inputs</>: ";
                for (var i = 0; i <= byte.MaxValue; i++)
                {
                    var mapped = ChannelMapping.MidiMapping((byte)i);
                    if (mapped == channel)
                    {
                        if (found == 0) o += i;
                        else o += ", " + i;
                        found += 1;
                    }
                }
                if (found == 0) o = $"<warningText>No MIDI inputs are mapped to this input channel ({channel}).</>";

                var desc = ChannelMapping.MarkupDescription(channel);
                if (desc != null)
                    o += "\n\n" + desc;

                return o;
            },
            Columns = ColumnBuilder.New<ChannelEquivalent>()
                .Add("Input", e => e.InputChannel.ToString())
                    .Width(200f)
                    .Modify(e =>
                    {
                        e.Key = "input";
                        if (Channel == null)
                        {
                            e.Format = (row, column, table) => new EnumAutocomplete<DrumChannel>(row.InputChannel)
                            {
                                RelativeSizeAxes = Axes.X,
                                Height = 30,
                                OnSelect = e =>
                                {
                                    Equivalents.Replace(row.InputChannel, row.OutputChannel, e.Value, row.OutputChannel);
                                    EquivalentsBindable.TriggerChange();
                                    row.InputChannel = e.Value;
                                }
                            };
                        }
                    })
                .Add("Equivalent", e => e.OutputChannel.ToString())
                    .Modify(e => e.Key = "output")
                    .Width(200f)
                    .Format((row, column, table) => new EnumAutocomplete<DrumChannel>(row.OutputChannel)
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        OnSelect = e =>
                        {
                            Equivalents.Replace(row.InputChannel, row.OutputChannel, row.InputChannel, e.Value);
                            EquivalentsBindable.TriggerChange();
                            row.OutputChannel = e.Value;
                        }
                    })
                .Add(null, e => null)
                    .Format((row, column, table) => new Container
                    {
                        Height = 30,
                        Width = 30,
                        Children = [
                            new IconButton(() =>
                            {
                                Equivalents.Remove(row.InputChannel, row.OutputChannel);
                                EquivalentsBindable.TriggerChange();
                                Table.InvalidateRows();
                            }, FontAwesome.Solid.Times, 20)
                            {
                                X = 5,
                                Y = 5,
                                MarkupTooltip = "Remove row",
                                BlockHover = false // keep row highlighted
                            }
                        ]
                    })
                    .Modify(e =>
                    {
                        e.ExactWidth = 30;
                        e.NoCellHover = true;
                    })
                .Build()
        }, BuildRows)
        {
            Y = 65
        });
    }

    List<ChannelEquivalent> BuildRows()
    {
        IEnumerable<(DrumChannel Input, DrumChannel Map)> res = Util.ConfigManager.ChannelEquivalents.Value;

        if (Channel is DrumChannel c)
            res = res.Where(e => e.Input == c);

        return res.Select(e => new ChannelEquivalent(e)).OrderBy(e => e.InputChannel).ThenBy(e => e.OutputChannel).ToList();
    }


    [BackgroundDependencyLoader]
    private void load()
    {
        AddFooterButtonSpaced(new DrumButton
        {
            Text = "Add",
            AutoSize = true,
            Action = () =>
            {
                var newChannel = Channel ?? DrumChannel.None;
                Equivalents.Add(newChannel, DrumChannel.None);
                EquivalentsBindable.TriggerChange();
                Table.InvalidateRows();
                Table.ValidateRows();
                var rows = Table.InternalRows;
                var newRowI = -1;
                for (var i = 0; i < rows.Count; i++)
                {
                    var e = rows[i];
                    if (e.InputChannel == newChannel && e.OutputChannel == DrumChannel.None)
                    {
                        Table.FlashRow(i);
                        newRowI = i;
                        break;
                    }
                }
                if (newRowI == -1) return;

                var targetColumn = Channel.HasValue ? "output" : "input";
                var dropdown = Table.GetCellContent(rows[newRowI], Table.Config.Columns.First(e => e.Key == targetColumn)) as IAcceptFocus;
                if (dropdown != null)
                    ScheduleAfterChildren(() => dropdown.Focus(GetContainingFocusManager()));
            }
        });
        AddFooterButtonSpaced(new DrumButton
        {
            Text = "Add Defaults",
            AutoSize = true,
            Action = () =>
            {
                Equivalents.AddDefaults(Channel);
                EquivalentsBindable.TriggerChange();
                Table.InvalidateRows();
                Table.ValidateRows();
            },
            MarkupTooltip = DefaultTooltip(Channel, true)
        });
        AddFooterButtonSpaced(new DrumButton
        {
            Text = "Reset to Default",
            AutoSize = true,
            Action = () =>
            {
                if (Channel is DrumChannel c)
                    Equivalents.ResetToDefault(c);
                else
                    Equivalents.ResetToDefault();
                EquivalentsBindable.TriggerChange();
                Table.InvalidateRows();
                Table.ValidateRows();
            },
            MarkupTooltip = DefaultTooltip(Channel, false)
        });
    }

    static string DefaultTooltip(DrumChannel? channel, bool add)
    {
        if (channel is DrumChannel c)
        {
            var defaults = ChannelEquivalents.Default.Where(e => e.Input == channel).ToList();
            if (defaults.Count == 0)
            {
                if (add) return $"{channel} has no default equivalents.";
                return $"{channel} has no default equivalents. All equivalents will be removed.";
            }
            var o = add ? $"These equivalents for {channel} will be added: " : $"The equivalents for {channel} will be set to:";
            foreach (var e in defaults)
            {
                if (e.Input == channel)
                    o += $"\n   {e.Map}";
            }
            return o;
        }
        else
        {
            if (add) return "This will add all default equivalents for all channels, without removing existings rows.";
            return "This will reset the equivalents for all channels.";
        }
    }
}