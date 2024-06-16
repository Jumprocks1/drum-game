
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osuTK.Graphics;

namespace DrumGame.Game.Components;

public class DrumButton : Button, IHasMarkupTooltip
{

    LocalisableString _text;
    public LocalisableString Text
    {
        get => SpriteText?.Text ?? _text;
        set
        {
            if (SpriteText != null)
                SpriteText.Text = value;
            else
                _text = value;
        }
    }

    bool _autoSize = false;
    public bool AutoSize
    {
        get => _autoSize; set
        {
            if (value == _autoSize) return;
            _autoSize = value;
            if (value)
            {
                AutoSizeAxes = Axes.X;
                Height = 30;
            }
        }
    }

    public float FontSize = 20;

    public Color4 BackgroundColour
    {
        get => Background.Colour;
        set => Background.FadeColour(value);
    }

    private Color4? flashColour;

    /// <summary>
    /// The colour the background will flash with when this button is clicked.
    /// </summary>
    public Color4 FlashColour
    {
        get => flashColour ?? BackgroundColour;
        set => flashColour = value;
    }

    /// <summary>
    /// The additive colour that is applied to the background when hovered.
    /// </summary>
    public Color4 HoverColour
    {
        get => Hover.Colour;
        set => Hover.FadeColour(value);
    }

    private Color4 disabledColour = Color4.Gray;

    /// <summary>
    /// The additive colour that is applied to this button when disabled.
    /// </summary>
    public Color4 DisabledColour
    {
        get => disabledColour;
        set
        {
            if (disabledColour == value)
                return;

            disabledColour = value;
            Enabled.TriggerChange();
        }
    }

    public double HoverFadeDuration { get; set; } = 200;
    public double FlashDuration { get; set; } = 200;
    public string MarkupTooltip { get; set; }
    public bool AutoFontSize;

    protected Box Hover;
    protected Box Background;
    protected SpriteText SpriteText;

    public DrumButton()
    {
        Background = new Box
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.Both,
            Colour = DrumColors.ActiveButton
        };
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        AddRange(new Drawable[]
        {
            Background,
            Hover = new Box
            {
                Alpha = 0,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White.Opacity(.1f),
                Blending = BlendingParameters.Additive
            },
        });
        SpriteText = CreateText();
        if (SpriteText != null)
            Add(SpriteText);

        Enabled.BindValueChanged(enabledChanged, true);
    }

    protected virtual SpriteText CreateText()
    {
        SpriteText text;
        if (AutoFontSize)
        {
            text = new AutoSizeSpriteText
            {
                MaxSize = FontSize,
                Padding = new MarginPadding { Horizontal = 5 }
            };
        }
        else text = new SpriteText();

        text.Depth = -1;
        text.Origin = Anchor.Centre;
        text.Anchor = Anchor.Centre;
        text.Font = FrameworkFont.Regular.With(size: FontSize);
        text.Colour = Colour4.White;
        text.Text = Text;
        if (AutoSize)
            text.Padding = new MarginPadding { Horizontal = 7 };
        return text;
    }

    protected override bool OnMouseDown(MouseDownEvent e) => true;

    protected override bool OnClick(ClickEvent e)
    {
        if (Enabled.Value)
            Background.FlashColour(FlashColour, FlashDuration);

        return base.OnClick(e);
    }

    protected override bool OnHover(HoverEvent e)
    {
        if (Enabled.Value)
            Hover.FadeIn(HoverFadeDuration);

        return base.OnHover(e);
    }

    protected override void OnHoverLost(HoverLostEvent e)
    {
        base.OnHoverLost(e);

        Hover.FadeOut(HoverFadeDuration);
    }

    private void enabledChanged(ValueChangedEvent<bool> e)
    {
        this.Colour = e.NewValue ? Color4.White : DisabledColour;
    }
}