using DrumGame.Game.Components.Basic;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osuTK.Graphics;

namespace DrumGame.Game.Components;

public class SearchTextBox : NonPositionalTextBox
{
    protected virtual float CaretWidth => 2;
    protected virtual Color4 SelectionColour => DrumColors.Selection;
    private readonly Box background;
    public Colour4 BackgroundColour { set => background.Colour = value; }
    public float TextContainerHeight { set => TextContainer.Height = value; }
    public SearchTextBox(string placeholderText = "Type to search", string value = null)
    {
        PlaceholderText = placeholderText;
        Add(background = new Box
        {
            RelativeSizeAxes = Axes.Both,
            Depth = 1,
            Colour = DrumColors.DarkActiveBackground,
        });
        TextContainer.Height = 0.8f;
        if (value != null)
        {
            Current.Value = value;
        }
    }

    public void AddHelpButton<T>(string name) where T : ISearchable<T>
    {
        Add(new IconButton(() =>
        {
            var modal = Util.Palette.Request(new Modals.RequestConfig
            {
                Title = $"{name} Info"
            });
            var paragraph = new TextFlowContainer();
            paragraph.AddParagraph("You can use advanced filters by specifying a field, operator and a search value. When specifying a field, you only need enough characters to uniquely identify the field. For example, you can filter difficulty with ");
            paragraph.AddText("d=3", e => { e.Colour = DrumColors.Code; });
            paragraph.AddText(".");
            paragraph.AddParagraph("");
            paragraph.AddParagraph("To search using multiple fields, add a space between filters (AND operation). To perform an OR operation, use | between filters.");
            paragraph.AddParagraph("");
            paragraph.AddParagraph("All fields can be used for filtering or sorting, the operator symbols are: =, !=, >, >=, <, <=, ^, ^^");
            paragraph.AddParagraph("");
            paragraph.AddParagraph("The following fields are available in this search bar (hover for more info + examples):");
            paragraph.RelativeSizeAxes = Axes.X;
            paragraph.AutoSizeAxes = Axes.Y;
            foreach (var field in T.Fields)
            {
                paragraph.AddParagraph<MarkupTooltipSpriteText>(field.Name, s =>
                {
                    s.MarkupTooltip = field.MarkupDescription;
                    s.Padding = new() { Left = 10 };
                });
            }
            modal.Add(paragraph);
        }, FontAwesome.Solid.Info, Height * 0.8f)
        {
            Origin = Anchor.CentreRight,
            Anchor = Anchor.CentreRight,
            MarkupTooltip = $"<command>View {name} Info</>"
        });
    }

    protected override void LoadAsyncComplete()
    {
        MoveCursorBy(Current.Value.Length); // can't put this in constructor since it uses a `Resolved` reference inside TextBox
        base.LoadAsyncComplete();
    }

    protected override void NotifyInputError() => background.FlashColour(Color4.Red, 200);
    protected override SpriteText CreatePlaceholder() => new SpriteText
    {
        Colour = DrumColors.Placeholder,
        Font = FrameworkFont.Condensed,
        Anchor = Anchor.CentreLeft,
        Origin = Anchor.CentreLeft,
        X = CaretWidth,
    };

    protected override Caret CreateCaret() => new DrumCaret
    {
        CaretWidth = CaretWidth,
        SelectionColour = SelectionColour,
    };
}
