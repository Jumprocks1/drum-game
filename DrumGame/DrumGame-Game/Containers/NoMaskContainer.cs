using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;

namespace DrumGame.Game.Containers;

public class NoMaskContainer : Container
{
    public override bool UpdateSubTreeMasking() => true;
}
