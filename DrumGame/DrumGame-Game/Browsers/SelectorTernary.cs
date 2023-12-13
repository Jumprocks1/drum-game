using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;

namespace DrumGame.Game.Browsers;

public partial class BeatmapSelector
{
    [CommandHandler]
    public bool ClearSearch(CommandContext _)
    {
        if (SearchInput.Current.Value != string.Empty)
        {
            SearchInput.Current.Value = string.Empty;
            return true;
        }
        return false;
    }

    class TernaryState
    {
        public int Value = 0;
        public int Place = 9;
        public Container Container;
        public TernaryState(Container container)
        {
            Container = container;
            Render();
        }
        char ch(int v) => v == 26 ? ' ' : (char)('a' + v);
        char cu(int v) => char.ToUpper(ch(v));
        void Render()
        {
            Container.Clear();
            Container.Add(new Box
            {
                Colour = HeaderBackground,
                RelativeSizeAxes = Axes.Both
            });
            var text0 = Place == 1 ? $"{cu(Value)}" : $"{cu(Value)}-{cu(Value + Place - 1)}";
            var text1 = Place == 1 ? $"{cu(Value + Place)}" : $"{cu(Value + Place)}-{cu(Value + 2 * Place - 1)}";
            var text2 = Place == 1 ? $"{cu(Value + 2 * Place)}" : $"{cu(Value + 2 * Place)}-{cu(Value + 3 * Place - 1)}";
            Container.Add(new CommandText(Command.Ternary0)
            {
                X = 5,
                Y = 2,
                Font = FrameworkFont.Regular.With(size: 26),
                Text = $"0 = {text0}"
            });
            Container.Add(new CommandText(Command.Ternary1)
            {
                X = 95,
                Y = 2,
                Font = FrameworkFont.Regular.With(size: 26),
                Text = $"1 = {text1}"
            });
            Container.Add(new CommandText(Command.Ternary2)
            {
                X = 185,
                Y = 2,
                Font = FrameworkFont.Regular.With(size: 26),
                Text = $"2 = {text2}"
            });
            Container.Add(new CommandIconButton(Command.ToggleTernary, FontAwesome.Solid.Times, 20)
            {
                X = 275,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft
            });
        }
        public void Reset()
        {
            Value = 0;
            Place = 9;
            Render();
        }
        public char PopChar()
        {
            var c = ch(Value);
            Reset();
            return c;
        }
        public bool Hit(int n) { Value += n * Place; Place /= 3; Render(); return Place == 0; }
    }
    void RemoveTernaryHandlers()
    {
        if (Ternary != null)
        {
            Util.CommandController.RemoveHandler(Command.Ternary0, Ternary0);
            Util.CommandController.RemoveHandler(Command.Ternary1, Ternary1);
            Util.CommandController.RemoveHandler(Command.Ternary2, Ternary2);
            Util.CommandController.RemoveHandler(Command.TernaryBackspace, Backspace);
        }
    }
    TernaryState Ternary;
    [CommandHandler]
    public void ToggleTernary()
    {
        if (Ternary != null)
        {
            RemoveTernaryHandlers();
            RemoveInternal(Ternary.Container, true);
            Ternary = null;
        }
        else
        {
            Ternary = new TernaryState(new Container
            {
                Width = BeatmapCarousel.Width,
                Height = 30,
                Y = HeaderHeight,
                Anchor = Anchor.TopRight,
                Origin = Anchor.TopRight
            });
            AddInternal(Ternary.Container);
            Util.CommandController.RegisterHandler(Command.Ternary0, Ternary0);
            Util.CommandController.RegisterHandler(Command.Ternary1, Ternary1);
            Util.CommandController.RegisterHandler(Command.Ternary2, Ternary2);
            Util.CommandController.RegisterHandler(Command.TernaryBackspace, Backspace);
        }
    }
    void Backspace() => SearchInput.Current.Value = string.IsNullOrWhiteSpace(SearchInput.Current.Value) ?
        string.Empty :
        SearchInput.Current.Value[..^1];
    void TernaryHit(int n) { if (Ternary.Hit(n)) SearchInput.Current.Value += Ternary.PopChar(); }
    void Ternary0() => TernaryHit(0);
    void Ternary1() => TernaryHit(1);
    void Ternary2() => TernaryHit(2);
}