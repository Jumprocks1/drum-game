using System;
using System.Linq;
using DrumGame.Game.Browsers.BeatmapSelection;
using DrumGame.Game.Commands;
using DrumGame.Game.Components;
using DrumGame.Game.Containers;
using DrumGame.Game.Modals;
using DrumGame.Game.Stores;
using DrumGame.Game.Utils;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;

namespace DrumGame.Game.Browsers;


public class CollectionManager : CompositeDrawable
{
    readonly BeatmapSelector Selector;
    BeatmapSelectorState State => Selector.State;
    CollectionStorage CollectionStorage => Selector.CollectionStorage;
    CommandButton Button;
    public CollectionManager(BeatmapSelector selector)
    {
        Selector = selector;
        AddInternal(new SpriteText
        {
            Text = "Current Collection",
            Y = 2,
            Font = FrameworkFont.Regular.With(size: 16),
            Anchor = Anchor.TopCentre,
            Origin = Anchor.TopCentre
        });
        Button = new CommandButton(Command.SelectCollection)
        {
            Y = 20,
            Height = 30,
            RelativeSizeAxes = Axes.X,
            FontSize = 26
        };
        Selector.State.CollectionBindable.BindValueChanged(BindingChanged, true);
        AddInternal(Button);
        Util.CommandController.RegisterHandlers(this);
    }

    void BindingChanged(ValueChangedEvent<string> e)
    {
        Button.Text = CollectionStorage.GetName(e.NewValue);
    }

    protected override void Dispose(bool isDisposing)
    {
        Selector.State.CollectionBindable.ValueChanged -= BindingChanged;
        Util.CommandController.RemoveHandlers(this);
        base.Dispose(isDisposing);
    }

    [CommandHandler]
    public bool NewCollection(CommandContext context) => context.Palette.Request(new RequestConfig
    {
        Title = "Creating New Empty Collection",
        Field = new StringFieldConfig("Name")
        {
            OnCommit = v => CollectionStorage.Save(MakeCollection(v))
        },
        Footer = new FooterCommand(Command.ConvertSearchToCollection)
    });

    Collection MakeCollection(string name)
    {
        if (name == null) return null;
        var filename = name.ToFilename(".json");
        if (CollectionStorage.Exists(filename))
        {
            Logger.Log($"Collection already exists at {filename}", level: LogLevel.Error);
            return null;
        }
        Logger.Log($"Creating new collection {filename}", level: LogLevel.Important);
        return Collection.NewEmpty(name, filename);
    }

    [CommandHandler]
    public bool ConvertSearchToCollection(CommandContext context)
    {
        var search = State.Search;

        return context.Palette.Request(new RequestConfig
        {
            Title = "Creating New Collection From Search",
            Fields = new IFieldConfig[] {
                new StringFieldConfig("Name"),
                new BoolFieldConfig {
                    Label = "Absolute",
                    Tooltip = new MultilineTooltipData(
                        "Checking this will cause the collection to be made from the exact current search results.\n" +
                        "Otherwise, the collection will be made using references to the current search and collection.")
                },
            },
            CommitText = "Create",
            OnCommit = request =>
            {
                var collection = MakeCollection(request.GetValue<string>(0));
                if (collection == null) return;
                var absolute = request.GetValue<bool>(1);
                if (absolute)
                {
                    var listRule = collection.Rules[0] as CollectionRuleList;
                    foreach (var map in Selector.FilteredMaps) listRule.List.Add(map.Filename);
                }
                else
                {
                    collection.Rules[0] = new CollectionRuleQuery(search);
                    var currentCollection = State.Collection;
                    if (currentCollection != null)
                        collection.Rules.Insert(0, new CollectionRuleRef(State.Collection));
                }
                CollectionStorage.Save(collection);
            }
        });
    }

    [CommandHandler]
    public bool SelectCollection(CommandContext context)
    {
        var def = new Collection { Name = "Default" };
        EnsureCollectionExists(context);
        context.GetItem(CollectionStorage.GetCollections().Select(e => CollectionStorage.GetCollection(e)).Prepend(def),
            e => e.Name, e =>
          {
              State.Collection = e.Source;
              Selector.UpdateFilter();
          }, "Selecting Collection", current: CollectionStorage.GetCollection(State.Collection));
        return true;
    }

    public bool ModifyCollection(CommandContext context, Collection collection, BeatmapSelectorMap targetMap, bool add)
    {
        var target = targetMap?.Filename;
        if (target == null || collection == null) return false;


        bool ReturnMessage(bool success)
        {
            if (success)
            {
                context.ShowMessage(add ? $"{target} added to {collection.Name}" :
                    $"{target} removed from {collection.Name}");
                CollectionStorage.Dirty(collection);
                Selector.CollectionChanged();
            }
            else
                context.ShowMessage(add ? $"{target} already in {collection.Name}" :
                    $"{target} not in {collection.Name}");
            return success;
        }

        bool CheckSuccess(bool changed = true)
        {
            var success = collection.Contains(targetMap, CollectionStorage) == add;
            if (success) ReturnMessage(changed);
            return success;
        }

        if (CheckSuccess(false)) return true;

        // first we remove all "negative" rules
        // these are rules that do the opposite of what we are trying to do, so we can safely remove them all
        var changed = false;
        foreach (var r in collection.Rules)
        {
            if (r is CollectionRuleList list)
            {
                if (add ? list.Op == CollectionRule.Operation.Not :
                    list.Op == CollectionRule.Operation.Or || list.Op == CollectionRule.Operation.And)
                {
                    var removed = list.List.Remove(target);
                    if (removed) changed = true;
                }
            }
        }

        if (changed && CheckSuccess()) return true;

        // adding is a little special because we can do this by having an `and` rule at the top of the collection
        // there is no equivalent for removing that I can think of
        if (add)
        {
            foreach (var r in collection.Rules)
                if (r is CollectionRuleList list && list.Op == CollectionRule.Operation.And)
                    if (list.List.Add(target) && CheckSuccess()) return true;
        }

        // now we will target the bottom most or/not
        CollectionRuleList targetRule = null;
        for (var i = collection.Rules.Count - 1; i >= 0; i--)
        {
            var rule = collection.Rules[i];
            if (rule is CollectionRuleList list)
                if ((add ? rule.Op == CollectionRule.Operation.Or : rule.Op == CollectionRule.Operation.Not) &&
                    !list.List.Contains(target))
                {
                    targetRule = list;
                    break;
                }
        }
        if (targetRule != null)
        {
            targetRule.List.Add(target);
            if (CheckSuccess()) return true;
        }

        // if we still haven't successed by just modifying old rules, we will have to add a rule at the bottom that guarentees success
        // after this, we are 100% certain that we have succeeded
        collection.Rules.Add(new CollectionRuleList
        {
            List = new() { target },
            Op = add ? CollectionRule.Operation.Or : CollectionRule.Operation.Not
        });
        return ReturnMessage(true);
    }

    [CommandHandler]
    public bool AddToCollection(CommandContext context)
    {
        var targetMap = context.TryGetParameter(out BeatmapSelectorMap o) ? o : Selector.TargetMap;
        var targetFile = targetMap?.Filename;
        if (targetFile == null) return false;
        EnsureCollectionExists(context);
        context.GetItem(CollectionStorage.GetCollections().Select(e => CollectionStorage.GetCollection(e)).Where(e => !e.Locked),
            e => e.Name, collection =>
        {
            if (collection.Locked) return;
            ModifyCollection(context, collection, targetMap, true);
        }, $"Adding {targetMap.LoadedMetadata.Title} to Collection", description: targetFile);
        return true;
    }
    [CommandHandler]
    public bool RemoveFromCollection(CommandContext context)
    {
        var col = CollectionStorage.GetCollection(Selector.State.Collection);
        if (col == null || col.Locked) return false;
        var targetMap = context.TryGetParameter(out BeatmapSelectorMap o) ? o : Selector.TargetMap;
        ModifyCollection(context, col, targetMap, false);
        return true;
    }

    public bool EnsureCollectionExists(CommandContext context) => context.TryGetParameter<string>(out var s) ? EnsureCollectionExists(s) : false;
    public bool EnsureCollectionExists(string name)
    {
        if (name == "Default") return false;
        var filename = name.ToFilename(".json");
        if (!CollectionStorage.Exists(filename))
        {
            Logger.Log($"Creating new collection {filename}", level: LogLevel.Important);
            CollectionStorage.Save(Collection.NewEmpty(name, filename));
            return true;
        }
        return false;
    }
}