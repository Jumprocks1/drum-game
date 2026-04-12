using System;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Midi;
using DrumGame.Game.Skinning;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osuTK;

namespace DrumGame.Game.Beatmaps.Display;

public class BeatmapPlayerInputDisplay : CompositeDrawable
{
    bool _visible = true;
    public bool Visible
    {
        get => _visible; set
        {
            if (value == _visible) return;
            _visible = value;
            Container.Alpha = value ? 1 : 0;
            Background.Alpha = value ? 1 : 0;
        }
    }

    Box Background;
    DrawSizePreservingFillContainer Container;
    public BeatmapPlayerInputDisplayInner Inner;
    public void Hit(DrumChannelEvent ev)
    {
        if (ev.MidiNote > 0)
        {
            var midi = Inner.MidiDisplays[ev.MidiNote];
            if (midi != null)
            {
                midi.Hit();
                return;
            }
        }
        Inner.ChannelDisplays[(int)ev.Channel]?.Hit();
    }
    public BeatmapPlayerInputDisplay()
    {
        RelativeSizeAxes = Axes.Both;
        SkinManager.SkinChanged += Load;
        DrumMidiHandler.AddAuxHandler(auxHandler);
        Load();
    }
    bool auxHandler(MidiAuxEvent ev)
    {
        // note, the Hit methods and stuff flow through TriggerEvent which runs safely on the update thread
        // these aux events come in on a different thread, so we have to schedule
        // it's not even worth checking if we're on the right thread since it will always be wrong
        if (ev is MidiControlEvent ce && ce.Control == Commons.Music.Midi.MidiCC.Foot)
            Schedule(() => Inner.PedalIndicator?.UpdateHihatPedalHeight(DrumMidiHandler.HiHatPosition));
        return false;
    }

    protected override void Dispose(bool isDisposing)
    {
        SkinManager.SkinChanged -= Load;
        DrumMidiHandler.RemoveAuxHandler(auxHandler);
        base.Dispose(isDisposing);
    }

    SkinNotationInputDisplayInfo SkinInfo => Util.Skin.Notation.InputDisplay;

    public void Load()
    {
        if (Background == null) AddInternal(Background = new Box { RelativeSizeAxes = Axes.Both });
        Background.Colour = SkinInfo.BackgroundColor;

        if (Container == null) AddInternal(Container = new DrawSizePreservingFillContainer());
        Container.TargetDrawSize = new Vector2(SkinInfo.BoundingBox.Width, SkinInfo.BoundingBox.Height);

        if (Inner != null) Container.Remove(Inner, true);

        Container.Child = Inner = new BeatmapPlayerInputDisplayInner(this)
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = Container.TargetDrawSize
        };
    }

    public enum HitType
    {
        Default,
        Bell,
        Edge
    }
    public class HittableDisplay
    {
        public readonly ChannelDisplay Display;
        public readonly HitType Type;
        public HittableDisplay(ChannelDisplay display, HitType type = HitType.Default)
        {
            Display = display; Type = type;
        }
        public void Hit()
        {
            if (Type == HitType.Default) Display.Hit();
            else if (Type == HitType.Edge) Display.HitEdge();
            else Display.HitBell();
        }
    }


    public class BeatmapPlayerInputDisplayInner : CompositeDrawable
    {
        public HittableDisplay[] ChannelDisplays = new HittableDisplay[(int)DrumChannel.Metronome];
        public HittableDisplay[] MidiDisplays = new HittableDisplay[128];
        public void Add(Drawable d) => AddInternal(d);

        public new readonly BeatmapPlayerInputDisplay Parent;
        public BeatmapPlayerInputDisplayInner(BeatmapPlayerInputDisplay parent) { Parent = parent; }
        public SkinNotationInputDisplayInfo SkinInfo => Parent.SkinInfo;
        public ChannelDisplay PedalIndicator;

        [BackgroundDependencyLoader]
        private void load()
        {
            for (var i = 0; i < SkinInfo.Displays.Count; i++)
            {
                var info = SkinInfo.Displays[i];
                var display = new ChannelDisplay(i, this, info);
                Add(display);
                if (info.Type == SkinNotationInputDisplayInfo.Skin_InputDisplayChannel.InputDisplayType.HiHatPedalIndicator)
                    PedalIndicator = display;

                if (info.Channels != null)
                    foreach (var channel in info.Channels)
                        ChannelDisplays[(int)channel] = new(display);
                if (info.MidiNotes != null)
                    foreach (var midi in info.MidiNotes)
                        MidiDisplays[midi] = new(display);

                if (info.EdgeChannels != null)
                    foreach (var channel in info.EdgeChannels)
                        ChannelDisplays[(int)channel] = new(display, HitType.Edge);
                if (info.EdgeMidiNotes != null)
                    foreach (var midi in info.EdgeMidiNotes)
                        MidiDisplays[midi] = new(display, HitType.Edge);

                if (info.BellChannels != null)
                    foreach (var channel in info.BellChannels)
                        ChannelDisplays[(int)channel] = new(display, HitType.Bell);
                if (info.BellMidiNotes != null)
                    foreach (var midi in info.BellMidiNotes)
                        MidiDisplays[midi] = new(display, HitType.Bell);

            }
        }
    }
    public class ChannelDisplay : CompositeDrawable
    {
        Vector2 InitialDown;
        protected override bool OnDragStart(DragStartEvent e)
        {
            if (e.AltPressed && e.Button == osuTK.Input.MouseButton.Left)
            {
                InitialDown = Info.Position;
                return true;
            }
            return base.OnDragStart(e);
        }
        protected override void OnDrag(DragEvent e)
        {
            var delta = e.MousePosition - e.MouseDownPosition;
            Info.Position = InitialDown + delta;
            Info.ApplyTo(this, Display.SkinInfo);
            Util.Skin.AddDirtyPath(SkinPathUtil.PathString(e => e.Notation.InputDisplay.Displays) + $"[{Index}]");
            base.OnDrag(e);
        }

        public Circle Overlay;

        public Drawable Main;
        public Drawable Border;
        public void UpdateFromSkin()
        {
            Info.ApplyTo(this, Display.SkinInfo);
            Main.Colour = Info.Color;
            // note, this border color inherits from the main color
            // this means a black object would not be able to have a border
            // drawable.BorderColour = BorderColor;
            // drawable.BorderThickness = BorderWidth;
        }

        public SkinNotationInputDisplayInfo.Skin_InputDisplayChannel Info;
        public BeatmapPlayerInputDisplayInner Display;
        int Index;
        public ChannelDisplay(int i, BeatmapPlayerInputDisplayInner display, SkinNotationInputDisplayInfo.Skin_InputDisplayChannel info)
        {
            Index = i;
            Display = display;
            Info = info;

            if (Info.BorderWidth > 0)
            {
                AddInternal(Border = new Circle
                {
                    Colour = Info.BorderColor,
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre
                });
            }
            if (info.Type == SkinNotationInputDisplayInfo.Skin_InputDisplayChannel.InputDisplayType.HiHatPedalIndicator)
            {
                AddInternal(Border = new Box { Colour = Info.BorderColor, RelativeSizeAxes = Axes.Both });
                AddInternal(Main = new Box
                {
                    Width = Info.Width - Info.BorderWidth * 2,
                    Height = Info.Height - Info.BorderWidth * 2,
                    X = Info.BorderWidth,
                    Y = Info.BorderWidth,
                });
            }
            else
            {
                AddInternal(Main = new Circle
                {
                    Width = Info.Width - Info.BorderWidth * 2,
                    Height = Info.Height - Info.BorderWidth * 2,
                    X = Info.BorderWidth,
                    Y = Info.BorderWidth,
                });
            }

            UpdateFromSkin();

            if (info.BellSize > 0)
            {
                Container kickPatch(float x) => new()
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    X = x,
                    Y = -info.BellSize / 2,
                    Size = new Vector2(info.BellSize),
                    CornerRadius = info.BellSize / 3,
                    Masking = true,
                    Colour = info.BellColor,
                    Child = new Box { RelativeSizeAxes = Axes.Both },
                };
                if (info.Type == SkinNotationInputDisplayInfo.Skin_InputDisplayChannel.InputDisplayType.DoubleKickDrum)
                {
                    AddInternal(LeftPatch = kickPatch(-info.BellSize * 0.6f));
                    AddInternal(RightPatch = kickPatch(info.BellSize * 0.6f));
                }
                else if (info.Type == SkinNotationInputDisplayInfo.Skin_InputDisplayChannel.InputDisplayType.KickDrum)
                {
                    AddInternal(RightPatch = kickPatch(0));
                }
                else
                {
                    AddInternal(Bell = new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(info.BellSize),
                        Colour = Info.BellColor,
                        Depth = -1
                    });
                }
            }
        }
        Container LeftPatch;
        Container RightPatch;
        Circle Bell;
        double lastHit;
        bool lastRight;



        public void HitBell()
        {
            if (BellOverlay == null)
            {
                AddInternal(BellOverlay = new()
                {
                    Colour = Colour4.PaleGreen,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(Info.BellSize),
                    Alpha = 0,
                    Depth = -2
                });
            }
            BellOverlay.ClearTransforms();
            BellOverlay.Alpha = Info.OverlayStrength ?? 0.5f;
            BellOverlay.FadeOut(300);
            BellOverlay.ScaleTo(1.3f, 50, Easing.OutQuint)
                .Then(e => e.ScaleTo(1, 150));
            Bell.ClearTransforms();
            Bell.ScaleTo(1.3f, 50, Easing.OutQuint)
                .Then(e => e.ScaleTo(1, 150));
        }

        public Circle BellOverlay;

        public void HitEdge()
        {
            if (Border == null) return;
            Border.ClearTransforms();
            Border.Colour = Info.BorderColor.Mix(Colour4.PaleGreen, 0.75f);
            Border.FadeColour(Info.BorderColor, 300);
            Border.ScaleTo(1.075f, 50, Easing.OutQuint)
                .Then(e => e.ScaleTo(1, 150));
        }

        public void UpdateHihatPedalHeight(byte pos)
        {
            var height = (float)pos / Util.Skin.Notation.InputDisplay.HiHatPedalMax;
            height = 1 - Math.Clamp(height, 0, 1);
            var baseHeight = Info.Height - Info.BorderWidth * 2;
            Main.Height = baseHeight * height;
            Main.Y = Info.BorderWidth;
        }

        public void Hit()
        {
            var delta = Clock.CurrentTime - lastHit;
            lastHit = Clock.CurrentTime;
            if (Overlay == null)
                AddInternal(Overlay = new() { RelativeSizeAxes = Axes.Both, Colour = Colour4.PaleGreen, Alpha = 0, Depth = -1 });
            Overlay.ClearTransforms();
            Overlay.Alpha = Info.OverlayStrength ?? 0.85f;
            Overlay.FadeOut(300);
            this.ScaleTo(1.1f, 50, Easing.OutQuint)
                .Then(e => e.ScaleTo(1, 150));

            void hitPatch(Container patch)
            {
                patch.ClearTransforms();
                patch.Colour = new Colour4(238, 238, 238, 255);
                patch.FadeColour(Info.BellColor, 70);
            }
            if (RightPatch != null)
            {
                lastRight = !(LeftPatch != null && lastRight && delta < 150);
                hitPatch(lastRight ? RightPatch : LeftPatch);
            }
        }
    }
}
