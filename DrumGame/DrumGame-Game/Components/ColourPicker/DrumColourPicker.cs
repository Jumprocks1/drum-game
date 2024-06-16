using DrumGame.Game.Commands;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osuTK;

namespace DrumGame.Game.Components.ColourPicker;

public class DrumColourPicker : osu.Framework.Graphics.UserInterface.ColourPicker
{
    public DrumColourPicker()
    {
        ((FillFlowContainer)InternalChild).Padding = new MarginPadding(2);
        AddInternal(new Box
        {
            RelativeSizeAxes = Axes.Both,
            Colour = DrumColors.DarkBorder,
            Depth = 1
        });
    }
    protected override HSVColourPicker CreateHSVColourPicker() => new DrumHSVColourPicker();
    protected override HexColourPicker CreateHexColourPicker() => new DrumHexColourPicker();

    public const float TotalHeight = 386;
}


public partial class DrumHSVColourPicker : HSVColourPicker
{
    public DrumHSVColourPicker()
    {
        Background.Colour = DrumColors.DarkBackground;

        Content.Padding = new MarginPadding(CommandPalette.Margin);
        Content.Spacing = new Vector2(0, 10);
    }

    protected override HueSelector CreateHueSelector() => new DrumHueSelector();
    protected override SaturationValueSelector CreateSaturationValueSelector() => new DrumSaturationValueSelector();

    public class DrumHueSelector : HueSelector
    {
        protected override Drawable CreateSliderNub() => new DrumHueSelectorNub();
    }

    public class DrumHueSelectorNub : CompositeDrawable
    {
        public DrumHueSelectorNub()
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Y,
                Width = 8,
                Height = 1.2f,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                    AlwaysPresent = true
                },
                Masking = true,
                BorderColour = DrumColors.Cyan,
                BorderThickness = 4
            };
        }
    }

    public class DrumSaturationValueSelector : SaturationValueSelector
    {
        protected override Marker CreateMarker() => new DrumMarker();

        private partial class DrumMarker : Marker
        {
            private readonly Box colourPreview;
            public DrumMarker()
            {
                InternalChild = new Container
                {
                    Size = new Vector2(15),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Masking = true,
                    BorderColour = DrumColors.BrightCyan,
                    BorderThickness = 4,
                    Child = colourPreview = new Box
                    {
                        RelativeSizeAxes = Axes.Both
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Current.BindValueChanged(_ => updatePreview(), true);
            }

            private void updatePreview() => colourPreview.Colour = Current.Value;
        }
    }
}



public class DrumHexColourPicker : HexColourPicker
{
    public DrumHexColourPicker()
    {
        Background.Colour = DrumColors.DarkBackground;

        Padding = new MarginPadding(CommandPalette.Margin);
        Spacing = 10;
    }

    protected override TextBox CreateHexCodeTextBox() => new DrumTextBox { Height = 30 };

    protected override ColourPreview CreateColourPreview() => new DrumColourPreview();

    private class DrumColourPreview : ColourPreview
    {
        private readonly Box previewBox;

        public DrumColourPreview()
        {
            InternalChild = previewBox = new Box
            {
                RelativeSizeAxes = Axes.Both
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(_ => updatePreview(), true);
        }

        private void updatePreview()
        {
            previewBox.Colour = Current.Value;
        }
    }
}
