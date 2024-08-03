using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DrumGame.Game.Beatmaps.Scoring;
using DrumGame.Game.Channels;
using DrumGame.Game.Components;
using DrumGame.Game.Skinning;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;

namespace DrumGame.Game.Beatmaps.Display.Mania;

public class ManiaCanvas : Canvas
{
    public class Data
    {
        public (int Start, int End) VisibleChips;
        public (int Start, int End) VisibleLines;
        public List<int> HiddenChips = new();
        public float Offset;
        public double TrackTime;
        public double UpdateTime;
        public JudgementEvent[] RecentJudgements;
        public class JudgementEvent // could probably be struct
        {
            public HitScoreRating Rating;
            public DrumChannel Channel;
            public double Error;
            public double UpdateTime; // update time since we want these to animate regardless of track time
            public JudgementEvent() { }
            public JudgementEvent(ScoreEvent e, double t)
            {
                Rating = e.Rating;
                UpdateTime = t;
                Channel = e.Channel;
                Error = e.HitError ?? double.NaN;
            }
        }
    }

    void ApplyStateInternal(Node node)
    {
        ApplyState(node.State);
        var track = Display.Track;
        node.TrackTime = track.CurrentTime;
        node.TrackBeat = track.CurrentBeat;
        node.MsPerBeat = track.CurrentBPM.MillisecondsPerQuarterNote;
    }
    readonly ManiaBeatmapDisplay Display;
    public ManiaCanvas(ManiaBeatmapDisplay display)
    {
        Display = display;
    }


    protected override DrawNode CreateDrawNode() => new Node(this);
    public Action<Data> ApplyState;
    public Action<Node, Data> Draw; // don't change this ever
    public class Node : CanvasNode
    {
        protected readonly new ManiaCanvas Source; // don't use this on draw thread
        public Data State = new();
        public Node(ManiaCanvas source) : base(source)
        {
            Source = source;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public record struct LaneParameters
        {
            public UniformFloat AspectRatio;
            public UniformInt Index;
            public UniformInt Channel;
            public UniformPadding4 Padding;
        }
        IUniformBuffer<LaneParameters> laneParameters;
        public void SetLaneParameters(ManiaBeatmapDisplay.Lane lane)
        {
            if (laneParameters != null) laneParameters.Data = new()
            {
                AspectRatio = Width * lane.Width / Height,
                Channel = (int)lane.Config.Channel,
                Index = lane.Config.LoadedIndex
            };
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public record struct NoteParameters
        {
            public UniformFloat AspectRatio;
            public UniformInt Channel;
            public UniformInt Modifiers;
            public UniformPadding4 Padding;
        }
        public void SetNoteParameters(ManiaBeatmapDisplay.Chip chip, bool adornment)
        {
            if (noteParameters != null)
            {
                var drawWidth = adornment ? chip.Width : chip.Width * chip.ChipWidth;
                noteParameters.Data = new()
                {
                    AspectRatio = Width * drawWidth / (Height * chip.Height),
                    Channel = (int)chip.Channel,
                    Modifiers = (int)chip.Modifiers
                };
            }
        }
        IUniformBuffer<NoteParameters> noteParameters;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public record struct JudgementParameters
        {
            public UniformFloat Age;
            public UniformInt Rating; // 0 = ignored, 1 = Perfect, 4 = Miss
            public UniformFloat Error; // >0 => Late, <0 => Early, NaN => not available (ie. an unplayed miss or ignore)
            public UniformInt Channel;
        }
        IUniformBuffer<JudgementParameters> judgementParameters;
        public void SetJudgementParameters(Data.JudgementEvent judgement)
        {
            if (judgementParameters != null) judgementParameters.Data = new()
            {
                Rating = (int)judgement.Rating,
                Error = (float)judgement.Error,
                Channel = (int)judgement.Channel,
                Age = (float)(State.UpdateTime - judgement.UpdateTime)
            };
        }
        protected override void BindUniformResources(IShader shader, IRenderer renderer)
        {
            base.BindUniformResources(shader, renderer);
            laneParameters ??= renderer.CreateUniformBuffer<LaneParameters>();
            shader.BindUniformBlock("m_LaneParameters", laneParameters);

            noteParameters ??= renderer.CreateUniformBuffer<NoteParameters>();
            shader.BindUniformBlock("m_NoteParameters", noteParameters);

            judgementParameters ??= renderer.CreateUniformBuffer<JudgementParameters>();
            shader.BindUniformBlock("m_JudgementParameters", judgementParameters);
        }
        public override void ApplyState()
        {
            base.ApplyState();
            Source.ApplyStateInternal(this);
        }
        protected override void Draw(IRenderer renderer)
        {
            base.Draw(renderer);
            Renderer = renderer;

            Color = DrawColourInfo.Colour; // have to reset color on each render
            Source.Draw(this, State);

            BoundShader?.Unbind();
            BoundShader = null;
            Renderer = null;
        }
    }
}