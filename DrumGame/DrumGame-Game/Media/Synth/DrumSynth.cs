using System;
using System.Buffers;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using ManagedBass;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace DrumGame.Game.Media.Synth;

public class DrumSynth : CompositeDrawable, IModal
{
    ClickBlockingContainer Container;
    public Action CloseAction { get; set; }

    const int SampleRate = 44100;
    // this needs to be a lot larger than I would normally expect
    public double playbackQueueSize = 0.1;
    // also queues this amount, so size will fluctuate between this and 2x;
    public int PlaybackQueueSamples => (int)(playbackQueueSize * SampleRate);

    long nextT = 0;


    int pushStreamHandle;
    int stalledSyncHandle;

    public DrumSynth()
    {
        RelativeSizeAxes = Axes.Both;
        AddInternal(Container = new ClickBlockingContainer { RelativeSizeAxes = Axes.Both });
        Container.Add(new Box
        {
            Colour = DrumColors.DarkBackground,
            RelativeSizeAxes = Axes.Both
        });
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        pushStreamHandle = Bass.CreateStream(SampleRate, 1, BassFlags.Float, StreamProcedureType.Push);
        putData(PlaybackQueueSamples * 2);
        Bass.ChannelPlay(pushStreamHandle);
        stalledSyncHandle = Bass.ChannelSetSync(pushStreamHandle, SyncFlags.Stalled, 0, (_, __, ___, ____) =>
        {
            Console.WriteLine("stalled");
        });
    }

    protected override void Dispose(bool isDisposing)
    {
        Bass.ChannelRemoveSync(pushStreamHandle, stalledSyncHandle);
        Bass.ChannelStop(pushStreamHandle); // TODO not sure if we even need this since we free below
        Bass.StreamFree(pushStreamHandle);
        pushStreamHandle = 0;
        base.Dispose(isDisposing);
    }

    void putData(int putCount)
    {
        var end = nextT + putCount;
        var buffer = ArrayPool<float>.Shared.Rent(putCount);
        for (var i = 0; i < putCount; i++)
            buffer[i] = audioRenderer(nextT + i);
        Bass.StreamPutData(pushStreamHandle, buffer, putCount * sizeof(float));
        ArrayPool<float>.Shared.Return(buffer);
        nextT += putCount;
    }

    protected override void Update()
    {
        var playbackBuffer = Bass.ChannelGetData(pushStreamHandle, 0, (int)DataFlags.Available);
        var queue = Bass.StreamPutData(pushStreamHandle, 0, 0);
        var bytesPerSample = sizeof(float); // I think can also pull this from the stream info
        var available = playbackBuffer + queue;
        var samples = available / bytesPerSample;
        if (samples < PlaybackQueueSamples)
            putData(PlaybackQueueSamples); // this will give very inconsistent latency, but that's fine
        base.Update();
    }


    float audioRenderer(long t)
    {
        var s = t / (double)SampleRate;

        var volume = 0.004;


        var rate = 2;
        var eT = (s * rate) % 1;
        var envelope = 1 / Math.Exp(eT * 5);
        var attack = 0.003;
        if (eT < attack)
            envelope *= (eT / attack);

        var sPi = s * Math.PI * 2;

        var o = Math.Sin(sPi * 100) + Math.Sin(sPi * 200) / 5 + Math.Sin(sPi * 300) / 5 / 5 + Math.Sin(sPi * 400) / 5 / 5 / 5;
        o *= volume * envelope;
        return (float)o;
    }
}