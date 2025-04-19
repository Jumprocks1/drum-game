using System;
using System.Linq;
using DrumGame.Game.Channels;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic.Autocomplete;
using DrumGame.Game.Input;
using DrumGame.Game.Midi;
using DrumGame.Game.Modals;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Views.Settings.SettingInfos;

public class MidiBindingRow
{
    public byte Note;
    public DrumChannel DrumChannel;
    public MidiBindingRow((byte, DrumChannel) e)
    {
        Note = e.Item1;
        DrumChannel = e.Item2;
    }
    public DrumButton ConfigButton;
    public Container Container;
    public IconButton AdvancedSettingsButton;

    public void Update()
    {
        ConfigButton.Enabled.Value = DrumChannel != DrumChannel.None;
        if (AdvancedSettingsButton != null)
        {
            Container.Remove(AdvancedSettingsButton, true);
            AdvancedSettingsButton = null;
        }
        if (AdvancedChannelSettings.SettingsFor(DrumChannel) is (Action, string) advancedSettings)
        {
            Container.Add(AdvancedSettingsButton = new IconButton(advancedSettings.Action, FontAwesome.Solid.Cog, 20)
            {
                X = 205,
                Y = 5,
                MarkupTooltip = advancedSettings.Tooltip,
                BlockHover = true
            });
        }
    }
}

public class MidiMappingView : RequestModal
{
    const string midiColumnKey = "midi";
    BindableMidiMapping Bindable => Util.ConfigManager.MidiMapping;
    MidiMapping Mapping => Bindable.Value;
    TableView<MidiBindingRow> Table;
    public MidiMappingView() : base(new RequestConfig
    {
        Title = "Editing MIDI Mapping",
        Description = "To start, hit the trigger that you want to map",
        Width = 780f
    })
    {
        Foreground.Add(new CommandIconButton(Commands.Command.MidiMonitor, FontAwesome.Solid.Music, 30)
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = 6,
            X = -6
        });
        Add(Table = new TableView<MidiBindingRow>(new()
        {
            RowHighlight = DrumColors.RowHighlight.MultiplyAlpha(0.5f),
            CellHighlight = DrumColors.BrightGreen.MultiplyAlpha(0.4f),
            CellPadding = new MarginPadding(2),
            ExtraCellTooltip = (column, row) =>
            {
                var channel = row.DrumChannel;
                var equivalents = Util.ConfigManager.ChannelEquivalents.Value.EquivalentsFor(channel)
                    .Where(e => e != DrumChannel.None).ToList();
                if (equivalents.Count == 0) return null;
                var o = $"\n{channel} has the following equivalents configured:";
                foreach (var eq in equivalents)
                {
                    o += $"\n   {eq}";
                }
                o += $"\nTriggering MIDI note {row.Note} will count for any of the above channels.";
                return o;
            },
            Columns = ColumnBuilder.New<MidiBindingRow>()
                .Add("MIDI Number", e => e.Note.ToString())
                    .Modify(e => e.Key = midiColumnKey)
                    .Format((row, column, table) =>
                    {
                        var textBox = new DrumTextBox
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 30,
                            Text = row.Note.ToString(),
                            CommitOnFocusLost = true,
                            SelectOnFocus = true
                        };
                        textBox.OnCommit += (e, _) =>
                        {
                            var oldValue = row.Note;
                            if (byte.TryParse(e.Current.Value, out var o) && o != oldValue)
                            {
                                var existing = Table.InternalRows.AsList().FindIndex(e => e.Note == o);
                                if (existing >= 0)
                                {
                                    Table.FlashRow(existing);
                                    ScheduleAfterChildren(() => textBox.Errored());
                                }
                                else
                                {
                                    row.Note = o;
                                    Mapping.Replace(oldValue, o, row.DrumChannel);
                                    Bindable.TriggerChange();
                                }
                            }
                            textBox.Text = row.Note.ToString();
                        };
                        return textBox;
                    })
                .Add("Mapped Drum Channel", e => e.DrumChannel.ToString())
                    .Width(200f)
                    .Format((row, column, table) => new EnumAutocomplete<DrumChannel>(row.DrumChannel)
                    {
                        RelativeSizeAxes = Axes.X,
                        Height = 30,
                        OnSelect = e =>
                        {
                            Bindable.Value.Replace(row.Note, row.Note, e.Value);
                            Bindable.TriggerChange();
                            row.DrumChannel = e.Value;
                            row.Update();
                        }
                    })
                .Add("Default Mapping", e => ChannelMapping.StandardMidiMapping(e.Note).ToString())
                    .Hide()
                .Add(null, e => null)
                    .Format((row, column, table) =>
                    {
                        row.ConfigButton = new DrumButton
                        {
                            Text = "Configure Equivalents",
                            Width = 170,
                            Height = 30,
                            X = 30,
                            Action = () => Util.Palette.Push(new ChannelEquivalentsView(row.DrumChannel))
                        };
                        row.Container = new Container
                        {
                            Height = 30,
                            RelativeSizeAxes = Axes.X,
                            Children = [
                                 new IconButton(() =>
                                {
                                    Mapping.Remove(row.Note);
                                    Table.InvalidateRows();
                                }, FontAwesome.Solid.Times, 20)
                                {
                                    X = 5,
                                    Y = 5,
                                    MarkupTooltip = "Remove row",
                                    BlockHover = false // keep row highlighted
                                },
                                row.ConfigButton
                             ]
                        };
                        row.Update();
                        return row.Container;
                    })
                    .Modify(e =>
                    {
                        e.ExactWidth = 230;
                        e.NoCellHover = true;
                    })
                .Build()
        }, () => Util.ConfigManager.MidiMapping.Value.Select(e => new MidiBindingRow(e)).ToList()));
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
                Mapping.Add(0, DrumChannel.None);
                Table.InvalidateRows();
                Table.ValidateRows();
                FlashNote(0);
                var textField = Table.GetCellContent(Table.InternalRows.FirstOrDefault(e => e.Note == 0),
                    Table.Config.Columns.First(e => e.Key == midiColumnKey));
                // not really sure why we need schedule, probably because button tries pulling focus
                // regular schedule doesn't work, so has to be after children
                if (textField != null)
                    ScheduleAfterChildren(() => GetContainingFocusManager().ChangeFocus(textField));
            }
        });
        AddFooterButtonSpaced(new DrumButton
        {
            Text = "Clear",
            AutoSize = true,
            Action = () =>
            {
                Mapping.Clear();
                Table.InvalidateRows();
            }
        });
        DrumMidiHandler.AddNoteHandler(OnMidiNote, true);
    }

    protected override void Dispose(bool isDisposing)
    {
        DrumMidiHandler.RemoveNoteHandler(OnMidiNote);
        base.Dispose(isDisposing);
    }

    bool OnMidiNote(MidiNoteOnEvent ev)
    {
        Schedule(() =>
        {
            var hasMapping = Mapping.HasMappingOverride(ev.Note);
            if (!hasMapping)
            {
                Mapping.Add(ev.Note, ChannelMapping.MidiMapping(ev.Note));
                Table.InvalidateRows();
                Table.ValidateRows();
                Table.FlashRow(Mapping.Count - 1);
            }
            else FlashNote(ev.Note);
        });
        return true;
    }

    void FlashNote(byte note)
    {
        var rows = Table.InternalRows;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Note == note)
            {
                Table.FlashRow(i);
                break;
            }
        }
    }
}
