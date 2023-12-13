using DrumGame.Game.Components;

namespace DrumGame.Game.Beatmaps.Display;

/* TODO
I would like to try particle effects
These don't exist in the framework, but `osu` does have it's own

osu uses a list of `ParticlePart`s that is cloned during apply state (using Clear and AddRange)

During blit, it simply goes through all particles and calls DrawQuad
This perfectly matches what we already do with our canvas, so no changes should be necessary

They perform 1 matrix mult for each particle, and they seem to also center each particle as expected

ParticlePart contains duration, direction, and distance - all three are random
    The current position is not stored, instead it's computed on each draw frame.
    I think this is a good way to do it, as long as the position math is simple
    It does do a Sin and Cos call each frame for no reason though. Storing direction as a vector would be better

There's also ParticleSpewer
    This uses an array instead of a list. To ApplyState it uses [].CopyTo([], 0)
    Particle has start time, start pos, velocity, duration, angles, and scale
    For actual position, it uses "calculus" to compute based on time

CursorTrail does something very similar to ParticleSpewer, although it does use a QuadBatch
    It also uses a custom uniform
    Not sure why it needs the quad batch since it still reuploads each frame
    Must be because the default vertex layout is insufficient

Triangle renderer is similar to CursorTrail
 */

public class NotationCanvas : Canvas<NotationCanvas.NotationCanvasData>
{
    public class NotationCanvasData
    {

    }
    public NotationCanvas(MusicNotationBeatmapDisplay display)
    {
        base.Draw = Draw;
        base.ApplyState = ApplyState;
    }

    new void ApplyState(NotationCanvasData data)
    {

    }

    new void Draw(Canvas<NotationCanvasData>.Node node, NotationCanvasData data)
    {

    }
}