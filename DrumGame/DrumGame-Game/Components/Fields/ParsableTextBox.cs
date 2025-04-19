using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;

namespace DrumGame.Game.Components.Fields;

public class ParsableTextBox<T> : DrumTextBox where T : IParsable<T>
{
    Bindable<T> Binding;
    public ParsableTextBox(Bindable<T> binding)
    {
        Binding = binding.GetBoundCopy();
        Binding.BindValueChanged(ev => Text = Binding.Value.ToString(), true);
    }
    protected override void Commit()
    {
        var error = false;
        if (T.TryParse(Current.Value, null, out var o))
        {
            Binding.Value = o;
        }
        else
        {
            error = true;
            Binding.Value = default;
        }
        // needed because the ValueChanged event may not trigger if the binding already matched
        Text = Binding.Value.ToString();
        base.Commit();
        // have to trigger after commit is complete, since that also sets the background color
        if (error) NotifyInputError();
    }
}