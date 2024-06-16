using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using DrumGame.Game.Skinning;
using osu.Framework.Graphics;

namespace DrumGame.Game.Beatmaps.Display.Mania;

public class LaneContainer : AdjustableSkinElement
{
    public IReadOnlyList<Drawable> Children => InternalChildren;
    public void Add(Drawable drawable) => AddInternal(drawable);
    public override Expression<Func<Skin, AdjustableSkinData>> SkinPathExpression => e => e.Mania.LaneContainer;

    public override AdjustableSkinData DefaultData() => new()
    {
        RelativeSizeAxes = Axes.Both,
        Origin = Anchor.TopCentre,
        Anchor = Anchor.TopCentre,
        Width = 0.55f,
    };

    public void RemoveAll(Predicate<Drawable> pred, bool disposeImmediately)
    {
        for (var i = 0; i < InternalChildren.Count; i++)
        {
            var tChild = InternalChildren[i];
            if (pred(tChild))
            {
                RemoveInternal(tChild, disposeImmediately);
                i--;
            }
        }
    }
}