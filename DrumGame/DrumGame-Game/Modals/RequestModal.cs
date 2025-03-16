using System;
using System.Collections.Generic;
using System.Linq;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osuTK.Input;

namespace DrumGame.Game.Modals;

public interface IFieldConfig
{
    public void TriggerCommit(object value) { }
    public IDrawableField Render(RequestModal modal);
    public Type OutputType { get; }
    public string Label { get; }
    public bool HasCommit { get; }
    public string Key { get; }
    public object Tooltip { get; }
    public string MarkupTooltip { get; }
    public Drawable[] LabelButtons { get; }
}
public interface IFieldConfig<T> : IFieldConfig // there might be some covariance stuff we can do here, but I couldn't figure it out
{
    public T Convert(object v);
    void IFieldConfig.TriggerCommit(object value) => OnCommit?.Invoke(Convert(value));
    public Action<T> OnCommit { get; set; }
    public T DefaultValue { get; }
    IDrawableField IFieldConfig.Render(RequestModal modal) => Render(modal);
    public new IDrawableField<T> Render(RequestModal modal);
}
public interface IDrawableField : IDrawable
{
    public object Value { get; } // this is hardly used, but still seems necessary
    public string Key => Config.Key;
    public FieldConfigBase Config { get; }
}
public interface IDrawableField<T> : IDrawableField
{
    object IDrawableField.Value => Value;
    public new T Value { get; set; }
}

public class FieldBuilder // could make a fancy field builder that keeps track of all the type parameters, ie. FieldBuilder<string,int,string,double>
{
    public FieldBuilder Add(IFieldConfig field)
    {
        Fields.Add(field);
        return this;
    }
    List<IFieldConfig> Fields = new();
    public IFieldConfig[] Build() => Fields.ToArray();
}

// TODO could make a reflection method for generating a RequestConfig from a class
// good example is DoubleBassStickingSettings
public class RequestConfig
{
    public float Width = 0.5f;
    public IFieldConfig[] Fields;
    public IFieldConfig Field { set { Fields = new[] { value }; } }
    public (Command, CommandHandlerWithContext)[] Commands;
    public string Title;
    public string Description;
    public string CommitText = "Okay";
    public string CloseText = "Close";
    public bool AutoFocus = true;
    public string DisableCommit; // set to tooltip text for disable reason
    public bool HasCommit => OnCommit != null || OnCommitBasic != null || (Fields != null && Fields.Any(e => e.HasCommit));
    public Action<RequestModal> OnCommit;
    // this handles a RequestResult instead of a request modal
    // this is useful when a request may be completed by a command context instead of a modal
    public Action<RequestResult> OnCommitBasic;
    public Func<RequestModal, bool> CanCommit;
    // works well as single button, footer command, or even container
    public Drawable Footer;
}

public class RequestResult
{
    public (string Key, object Value)[] Results;
    public RequestResult(IList<(string Key, object Value)> results)
    {
        Results = new (string Key, object Value)[results.Count];
        for (var i = 0; i < results.Count; i++)
            Results[i] = results[i];
    }
    public RequestResult(RequestModal requestModal)
    {
        var f = requestModal.Fields;
        Results = new (string Key, object Value)[f.Length];
        for (var i = 0; i < f.Length; i++)
            Results[i] = (f[i].Key, f[i].Value);
    }
    public T GetValue<T>(int i) => (T)Results[i].Value;
    public T GetValue<T>() => (T)Results[0].Value;
    public T GetValue<T>(string key) => (T)Results.First(e => e.Key == key).Value;
}

public class FooterCommand : CommandButton
{
    public FooterCommand(Command command) : this(Util.CommandController[command]) { }
    public FooterCommand(CommandInfo command) : this(command, command.Name) { }
    public FooterCommand(CommandInfo command, string text) : base(command)
    {
        AutoSize = true;
        Text = text;
        AfterActivate = () =>
        {
            this.FindClosestParent<RequestModal>()?.Close();
        };
    }
}

// should probably make a simple base class for this without close button or any request stuff
// basically just RequestConfig.Title
public class RequestModal : TabbableContainer, IModal, IAcceptFocus
{
    class MainContainer : FillFlowContainer
    {
        protected override bool Handle(UIEvent e)
        {
            if (e is MouseEvent)
            {
                if (e is DragStartEvent) return false;
                return true;
            }
            return base.Handle(e);
        }
    }

    public Action CloseAction { get; set; }
    public override bool CanBeTabbedTo => false;
    protected override Container<Drawable> Content => InnerContent;
    Container FooterButtonContainer;
    public Container InnerContent;
    public Drawable initialFocus; // TODO make private


    public readonly RequestConfig Config;

    public IDrawableField[] Fields { get; private set; }
    public void SetValue<T>(int i, T value) => ((IDrawableField<T>)Fields[i]).Value = value;
    public T GetValue<T>(int i) => ((IDrawableField<T>)Fields[i]).Value;
    public T GetValue<T>() => ((IDrawableField<T>)Fields[0]).Value;
    public T GetValue<T>(string key) => ((IDrawableField<T>)this[key]).Value;
    public IDrawableField GetField(string key)
    {
        foreach (var field in Fields)
        {
            if (field.Key == key)
                return field;
        }
        return null;
    }
    public T GetValueOrDefault<T>(string key)
    {
        var field = GetField(key);
        if (field == null) return default;
        return ((IDrawableField<T>)GetField(key)).Value;
    }
    public IDrawableField this[string key] => GetField(key) ?? throw new KeyNotFoundException($"Key not found: {key}");


    public RequestModal(string title, string description = null) : this(new RequestConfig
    {
        Title = title,
        Description = description,
    })
    { }
    protected override void Dispose(bool isDisposing)
    {
        if (Config.Commands != null)
            foreach (var command in Config.Commands)
                Util.CommandController.RemoveHandler(command.Item1, command.Item2);
        base.Dispose(isDisposing);
    }
    protected ModalForeground Foreground;
    public RequestModal(RequestConfig config)
    {
        Config = config;
        TabbableContentContainer = this;
        RelativeSizeAxes = Axes.Both;
        AddInternal(new ModalBackground(Close));
        var autoWidth = config.Width == 0;
        var autoSizeAxes = autoWidth ? Axes.Both : Axes.Y;
        var relativeAxes = autoWidth ? Axes.None : Axes.X;
        var mainContainer = new MainContainer
        {
            AutoSizeAxes = autoSizeAxes,
            RelativeSizeAxes = relativeAxes,
            Direction = FillDirection.Vertical,
            Padding = new MarginPadding(CommandPalette.Margin)
        };
        if (config.Title != null)
        {
            var titleHeight = 28;
            mainContainer.Add(new AutoSizeSpriteText
            {
                Origin = Anchor.TopCentre,
                Anchor = Anchor.TopCentre,
                Font = FrameworkFont.Regular.With(size: titleHeight),
                MaxSize = titleHeight,
                Text = config.Title,
                Padding = new MarginPadding { Bottom = CommandPalette.Margin }
            });
        }
        if (config.Description != null)
        {
            mainContainer.Add(new AutoSizeSpriteText
            {
                Origin = Anchor.TopCentre,
                Anchor = Anchor.TopCentre,
                Font = FrameworkFont.Regular.With(size: 16),
                MaxSize = 16,
                Text = config.Description,
            });
        }
        mainContainer.Add(InnerContent = new Container
        {
            AutoSizeAxes = autoSizeAxes,
            RelativeSizeAxes = relativeAxes
        });

        var hasCloseButton = Config.CloseText != null;
        var hasCommitButton = Config.CommitText != null && Config.HasCommit;
        if (hasCloseButton || hasCommitButton)
        {
            FooterButtonContainer = new Container
            {
                Padding = new MarginPadding { Top = CommandPalette.Margin },
                AutoSizeAxes = Axes.Y,
                RelativeSizeAxes = Axes.X,
                Origin = Anchor.TopCentre,
                Anchor = Anchor.TopCentre
            };
            if (hasCloseButton)
            {
                FooterButtonContainer.Add(new CommandButton(Command.Close)
                {
                    Text = Config.CloseText,
                    Width = 100,
                    Height = 30,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight
                });
            }
            if (hasCommitButton)
            {
                FooterButtonContainer.Add(new DrumButtonTooltip
                {
                    X = -105,
                    Text = Config.CommitText,
                    TooltipText = Config.DisableCommit ?? "(Enter)",
                    Width = 100,
                    Height = 30,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Action = Config.DisableCommit == null ? () => Commit() : null
                });
            }
            mainContainer.Add(FooterButtonContainer);
        }

        var relativeWidth = config.Width <= 1 && !autoWidth;
        var borderPadding = 2;

        Foreground = new ModalForeground(autoSizeAxes)
        {
            RelativeSizeAxes = relativeWidth ? Axes.X : Axes.None,
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
        };
        if (!autoWidth)
            Foreground.Width = relativeWidth ? config.Width : config.Width + (borderPadding + CommandPalette.Margin) * 2;

        Foreground.Add(mainContainer);
        AddInternal(Foreground);
        if (Config.Commands != null)
            foreach (var command in Config.Commands)
                Util.CommandController.RegisterHandler(command.Item1, command.Item2);
    }

    [BackgroundDependencyLoader]
    private void load()
    {
        var y = 0f;
        if (Config.Fields != null)
        {
            var len = Config.Fields.Length;
            Fields = new IDrawableField[len];
            var grid = new GridContainer { RelativeSizeAxes = Axes.X };
            var content = new Drawable[len][];
            var rows = new Dimension[len];
            for (var i = 0; i < len; i++)
            {
                var fieldInfo = Config.Fields[i];
                Fields[i] = fieldInfo.Render(this);
                var drawable = (Drawable)Fields[i];
                y += CommandPalette.Margin + drawable.Height;
                Drawable label = null;
                if (fieldInfo.Label != null)
                {
                    label = new MarkupTooltipSpriteText
                    {
                        Text = fieldInfo.Label + ": ",
                        MarkupTooltip = fieldInfo.MarkupTooltip,
                        Origin = Anchor.CentreLeft,
                        Y = drawable.Height / 2,
                    };
                    var buttons = fieldInfo.LabelButtons;
                    if (buttons != null)
                    {
                        var cont = new FillFlowContainer
                        {
                            Direction = FillDirection.Horizontal,
                            AutoSizeAxes = Axes.Both,
                            Origin = Anchor.CentreLeft,
                            Y = drawable.Height / 2,
                        };
                        label.Y = 0;
                        label.Anchor = Anchor.CentreLeft;
                        cont.Add(label);
                        cont.AddRange(buttons);
                        label = cont;
                    }
                }
                content[i] = new Drawable[] {
                    label,
                    drawable
                };
                if (Config.AutoFocus)
                    initialFocus ??= drawable;
                rows[i] = new Dimension(GridSizeMode.Absolute, i == len - 1 ? drawable.Height : drawable.Height + CommandPalette.Margin);
            }
            grid.Height = y - CommandPalette.Margin;
            grid.RowDimensions = rows;
            grid.ColumnDimensions = new[] { new Dimension(GridSizeMode.AutoSize) };
            grid.Content = content;
            Add(grid);
        }
        if (Config.Footer != null)
            AddFooterButton(Config.Footer);
    }

    public bool Commit()
    {
        if (!(Config?.CanCommit?.Invoke(this) ?? true)) return true;
        if (Config.Fields != null)
        {
            var fields = Config.Fields;
            for (var i = 0; i < fields.Length; i++)
                fields[i].TriggerCommit(Fields[i].Value);
        }
        Config.OnCommit?.Invoke(this);
        Config.OnCommitBasic?.Invoke(new(this));
        Close();
        return true;
    }
    public void Close() => CloseAction?.Invoke();
    protected override bool OnKeyDown(KeyDownEvent e)
    {
        if (e.Key == Key.Enter || e.Key == Key.KeypadEnter)
        {
            if (Commit()) return true;
        }
        return base.OnKeyDown(e);
    }
    public void Focus(IFocusManager focusManager)
    {
        if (initialFocus is IAcceptFocus f) f.Focus(focusManager);
        else focusManager.ChangeFocus(initialFocus);
    }

    // this is probably kinda bad but who cares
    public static implicit operator bool(RequestModal _) => true;

    // should try to stop using this one
    public void AddFooterButton(Drawable button) => FooterButtonContainer.Add(button);
    public void AddFooterButtonSpaced(Drawable button, float spacing = 5)
    {
        var pos = 0f;
        foreach (var child in FooterButtonContainer.Children)
        {
            if (child.Anchor.HasFlag(Anchor.x0))
            {
                var right = child.X + child.Width + spacing;
                if (right > pos)
                    pos = right;
            }
        }
        button.X = pos;
        FooterButtonContainer.Add(button);
    }
    public void AddCommandButtons(Command command, Func<CommandInfo, string> text = null)
    {
        var container = new FillFlowContainer
        {
            Direction = FillDirection.Full,
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Y = 35,
            Spacing = new osuTK.Vector2(5)
        };

        var commands = Util.CommandController.GetParameterCommands(command);
        foreach (var c in commands)
        {
            container.Add(new CommandButton(c)
            {
                Text = text?.Invoke(c) ?? c.Name,
                Height = 30,
                Width = 100,
                AfterActivate = CloseAction
            });
        }
        Add(container);
    }

    public void AddWarning(string message)
    {
        var y = InnerContent.Children.Sum(e => e.Height);
        var s = new SpriteText
        {
            Text = message,
            Colour = DrumColors.WarningText,
            Font = FrameworkFont.Regular,
            Y = y
        };
        Add(s);
    }
}