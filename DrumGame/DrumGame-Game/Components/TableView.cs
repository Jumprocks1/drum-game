using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DrumGame.Game.Components.Basic;
using DrumGame.Game.Containers;
using DrumGame.Game.Interfaces;
using DrumGame.Game.Utils;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Layout;

namespace DrumGame.Game.Components;

public static class ColumnBuilder
{
    public static ColumnBuilder<T> New<T>() => new();
}
public class ColumnBuilder<T>
{
    List<TableViewColumn<T>> items = new();
    public ColumnBuilder<T> Add(TableViewColumn<T> column)
    {
        items.Add(column);
        return this;
    }
    public ColumnBuilder<T> Add(string header, Func<T, string> getter) =>
        Add(new TableViewColumn<T>() { Header = header, GetValue = getter });
    public ColumnBuilder<T> Add(string fieldName)
    {
        var field = typeof(T).GetField(fieldName);
        if (field != null)
        {
            return Add(new TableViewColumn<T>
            {
                Header = field.Name,
                GetValue = e => field.GetValue(e)?.ToString(),
                SetValue = (e, v) =>
                {
                    try
                    {
                        // note, we don't use invariant culture here
                        field.SetValue(e, Convert.ChangeType(v, field.FieldType));
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Util.Palette.UserError(ex);
                        return false;
                    }
                }
            });
        }
        throw new Exception($"Field not found: {fieldName}");
    }
    public ColumnBuilder<T> Editable(Action<T, TableViewColumn<T>, TableView<T>> edit)
    {
        items[^1].Edit = edit;
        return this;
    }
    public ColumnBuilder<T> Editable(Action<T> edit)
    {
        items[^1].Edit = (e, _, __) => edit(e);
        return this;
    }
    public ColumnBuilder<T> BasicEdit()
    {
        var column = items[^1];
        column.Edit = (e, column, table) =>
        {
            Util.Palette.RequestString($"Changing {column.Header} for {e}", column.Header, column.GetValue(e), newValue =>
            {
                if (column.SetValue != null)
                {
                    if (column.SetValue(e, newValue))
                        table.UpdateCell(e, column);
                }
            });
        };
        return this;
    }
    public ColumnBuilder<T> Hide(bool hide = true)
    {
        items[^1].Hidden = hide;
        return this;
    }
    public TableViewColumn<T>[] Build() => [.. items];
}
public class TableViewColumn<T>
{
    // could also add GetTooltip or GetDrawable
    // we would change GetValue to setter only and then have it override GetDrawable if needed
    public Func<T, string> GetValue;
    // could also add HeaderDrawable, similiar to GetDrawable
    public string Header;
    public string HeaderMarkupTooltip;
    public bool Hidden;
    public Action<T, TableViewColumn<T>, TableView<T>> Edit;
    public bool Editable => Edit != null;
    public Func<T, object, bool> SetValue;

    public Drawable GetDrawable(TableViewConfig<T> config, T row)
    {
        return new SpriteText
        {
            Text = GetValue(row),
            Font = config.Font,
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft
        };
    }
}
public class TableViewConfig<T>
{
    public TableViewColumn<T>[] Columns;
    // if Data.Count * RowHeight is greater than MaxHeight, we will use a scroll container and set the height to this value
    // to never use scroll, set to null
    public float? MaxHeight = 29 * 14.5f;
    public float RowMinHeight; // 0 => full auto size
    public FontUsage HeaderFont = DrumFont.Bold.With(size: 24);
    public FontUsage Font = DrumFont.Regular.With(size: 18);
    public MarginPadding CellPadding = new MarginPadding
    {
        Horizontal = 4,
        Vertical = 1
    };

    public float HeaderBorderWidth = 1.5f;
    // https://stackoverflow.com/questions/7242909/moving-elements-in-array-c-sharp
    public void MoveColumn(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex) return;
        var temp = Columns[oldIndex];
        if (newIndex < oldIndex)
            Array.Copy(Columns, newIndex, Columns, newIndex + 1, oldIndex - newIndex);
        else
            Array.Copy(Columns, oldIndex + 1, Columns, oldIndex, newIndex - oldIndex);
        Columns[newIndex] = temp;
    }

    public Func<ContextMenuBuilder<T>, ContextMenuBuilder<T>> BuildContextMenu;
}

public class TableView<T> : CompositeDrawable
{
    class TableCell : CompositeDrawable, IHasMarkupTooltip, IHasContextMenu
    {
        readonly int Row; // -1 = header
        T RowData => Row == -1 ? default : Table.Rows[Row];
        readonly int VisibleColumn;
        readonly int Column;
        TableView<T> Table;
        TableViewConfig<T> Config => Table.Config;
        public TableViewColumn<T> ColumnConfig => Config.Columns[Column];

        public string MarkupTooltip
        {
            get
            {
                if (IsDisposed) return null;
                if (Row == -1) return "Right click for options";
                var o = "";
                var self = Config.Columns[Column];
                if (self.Edit != null)
                {
                    o += $"Left click to set <brightCyan>{self.Header}</c>\n\n";
                }
                for (var i = 0; i < Config.Columns.Length; i++)
                {
                    var newLine = i == Config.Columns.Length - 1 ? "" : "\n";
                    var column = Config.Columns[i];
                    var value = column.GetValue(Table.Rows[Row]);
                    if (i == Column)
                        o += $"<brightCyan>{column.Header}</c>: {value}{newLine}";
                    else
                        o += $"<brightGreen>{column.Header}</c>: {value}{newLine}";
                }
                return o;
            }
        }

        public MenuItem[] ContextMenuItems
        {
            get
            {
                if (Row == -1)
                {
                    var clickedColumn = Config.Columns[Column];
                    var builder = new ContextMenuBuilder<TableViewColumn<T>>(clickedColumn)
                        .Add($"Hide {clickedColumn.Header} Column", e =>
                        {
                            e.Hidden = true;
                            Table.Reload();
                        })
                        .Color(DrumColors.WarningText)
                        .Disabled(Table.VisibleColumns.Count <= 1 ? "Cannot hide last column" : null);

                    for (var i = 0; i < Config.Columns.Length; i++)
                    {
                        var column = Config.Columns[i];
                        // capture for lambda
                        var j = i;
                        if (column.Hidden)
                            builder
                                .Add($"Show {column.Header} Column", _ =>
                                {
                                    column.Hidden = false;
                                    if (j > Column) Config.MoveColumn(j, Column + 1);
                                    else Config.MoveColumn(j, Column);
                                    Table.Reload();
                                })
                                .Color(DrumColors.BrightGreen);
                    }
                    return builder.Build();
                }
                var rowContextMenuBuilder = ContextMenuBuilder.New(RowData);
                if (Config.BuildContextMenu != null)
                    rowContextMenuBuilder = Config.BuildContextMenu.Invoke(rowContextMenuBuilder);
                foreach (var column in Config.Columns)
                    if (column.Editable)
                        rowContextMenuBuilder
                            .Add($"Edit {column.Header}", e => column.Edit(e, column, Table))
                            .Color(DrumColors.BrightYellow);
                var menu = rowContextMenuBuilder.Build();
                if (menu.Length > 0)
                    return menu;
                return null;
            }
        }

        public TableCell(Drawable child, TableView<T> table, int row, int visibleColumn, int column)
        {
            Row = row;
            VisibleColumn = visibleColumn;
            Column = column;
            Table = table;
            Child = child;
            AddInternal(child);
            Padding = Config.CellPadding;
        }
        public Drawable Child;

        public void UpdateCell(Drawable child)
        {
            RemoveInternal(Child, true);
            AddInternal(Child = child);
        }

        protected override bool OnHover(HoverEvent e)
        {
            Table.Hover(Row, VisibleColumn);
            return base.OnHover(e);
        }
        protected override bool OnClick(ClickEvent e)
        {
            if (Row == -1) // header clicked, could sort
            { }
            else
            {
                if (ColumnConfig.Edit != null)
                {
                    ColumnConfig.Edit(Table.Rows[Row], ColumnConfig, Table);
                    return true;
                }
            }
            return base.OnClick(e);
        }
        protected override void OnHoverLost(HoverLostEvent e)
        {
            Table.Unhover(Row, VisibleColumn);
        }

        public void Add(Box hover)
        {
            AddInternal(hover);
        }
        public void Remove(Box hover)
        {
            RemoveInternal(hover, false);
        }
    }
    public readonly TableViewConfig<T> Config;
    FontUsage Font => Config.Font;
    List<TableViewColumn<T>> VisibleColumns;
    List<T> Rows;
    readonly Func<List<T>> GetRows;

    bool RowsValid; // might be able to skip this and just set Rows to null
    public void InvalidateRows()
    {
        RowsValid = false;
        // Note, we don't set Rows to null right away. It causes crashes for things like tooltips
    }

    Container<Drawable> BodyContainer;

    readonly LayoutValue columnWidths = new(Invalidation.DrawSize);
    public TableView(TableViewConfig<T> config, List<T> rows) : this(config, () => rows) { }
    public TableView(TableViewConfig<T> config, Func<List<T>> getRows)
    {
        Config = config;
        AutoSizeAxes = Axes.Y;
        RelativeSizeAxes = Axes.X;
        GetRows = getRows;
        AddLayout(columnWidths);
    }

    void ValidateRows()
    {
        if (IsDisposed) return;
        if (!RowsValid)
        {
            Rows = GetRows();
            RowsValid = true;
            Reload();
        }
    }

    public void Reload()
    {
        if (IsDisposed) return;
        foreach (var child in InternalChildren.ToList())
            RemoveInternal(child, true);
        CellHover?.Dispose();
        CellHover = null;
        mainLoad();
    }
    public void UpdateRow(T row)
    {
        var rowI = Rows.IndexOf(row);
        for (var colI = 0; colI < VisibleColumns.Count; colI++)
            Content[rowI + 1, colI].UpdateCell(VisibleColumns[colI].GetDrawable(Config, row));
        columnWidths.Invalidate();
    }
    public void UpdateCell(T row, TableViewColumn<T> column)
    {
        var rowI = Rows.IndexOf(row);
        var colI = VisibleColumns.IndexOf(column);
        if (colI == -1) return;
        Content[rowI + 1, colI].UpdateCell(column.GetDrawable(Config, row));
        columnWidths.Invalidate();
    }
    protected override void Update()
    {
        base.Update();
        ValidateRows();
        if (!columnWidths.IsValid)
        {
            var requestedColumnWidths = new float[VisibleColumns.Count];
            var xPadding = Config.CellPadding.TotalHorizontal;

            for (var i = 0; i < Content.GetLength(0); i++)
            {
                for (var j = 0; j < Content.GetLength(1); j++)
                {
                    var content = Content[i, j].Child;
                    if (content.Width + xPadding > requestedColumnWidths[j])
                        requestedColumnWidths[j] = content.Width + xPadding;
                }
            }

            var total = requestedColumnWidths.Sum();
            // if this is negative, we have some problems
            var add = (DrawWidth - total) / requestedColumnWidths.Length;
            for (var row = 0; row < Content.GetLength(0); row++)
            {
                var x = 0f;
                for (var col = 0; col < VisibleColumns.Count; col++)
                {
                    var width = requestedColumnWidths[col] + add;
                    Content[row, col].Width = width;
                    Content[row, col].X = x;
                    x += width;
                }
            }
            columnWidths.Validate();
        }
    }

    Box RowHover;
    Box CellHover;


    public (int, int)? CurrentHover;
    public void Hover(int row, int col)
    {
        CurrentHover = (row, col);
        CellHover.Alpha = 1;
        if (row >= 0)
        {
            RowHover.Alpha = 1;
            RowHover.Y = Content[row + 1, 0].Y;
            RowHover.Height = Content[row + 1, 0].Height;
        }
        if (CellHover.Parent != null)
            ((TableCell)CellHover.Parent).Remove(CellHover);
        if (Content[row + 1, col].ColumnConfig.Editable)
            CellHover.Colour = DrumColors.BrightCyan.MultiplyAlpha(0.1f);
        else
            CellHover.Colour = DrumColors.RowHighlight;
        Content[row + 1, col].Add(CellHover);
    }

    protected override void Dispose(bool isDisposing)
    {
        CellHover?.Dispose();
        base.Dispose(isDisposing);
    }
    public void Unhover(int row, int col)
    {
        if (CurrentHover == (row, col))
        {
            if (CellHover.Parent != null)
                ((TableCell)CellHover.Parent).Remove(CellHover);
            CurrentHover = null;
            RowHover.Alpha = 0;
        }
    }

    TableCell[,] Content; // includes header

    void mainLoad()
    {
        VisibleColumns = Config.Columns.Where(e => !e.Hidden).ToList();
        var allColumns = Config.Columns;
        AddInternal(BodyContainer = new()
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y
        });
        Content = new TableCell[Rows.Count + 1, VisibleColumns.Count];

        var yPadding = Config.CellPadding.TotalVertical;
        var y = 0f;

        var rowMaxHeight = 0f;
        var currentRow = -1; // -1 = header

        void setHeight(float height)
        {
            for (var i = 0; i < VisibleColumns.Count; i++)
                Content[currentRow + 1, i].Height = height;
        }
        void addCell(Drawable content, int visibleIndex, int col)
        {
            var cell = new TableCell(content, this, currentRow, visibleIndex, col)
            {
                Y = y
            };
            if (currentRow == -1) AddInternal(cell);
            else BodyContainer.Add(cell);
            Content[currentRow + 1, visibleIndex] = cell;
            if (content.Height + yPadding > rowMaxHeight)
                rowMaxHeight = content.Height + yPadding;
        }

        // headers
        float headerHeight;
        {
            var visibleIndex = 0;
            for (var col = 0; col < allColumns.Length; col++)
            {
                var column = allColumns[col];
                if (column.Hidden) continue;
                var sprite = new SpriteText
                {
                    Text = column.Header,
                    Font = Config.HeaderFont,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft
                };
                addCell(sprite, visibleIndex, col);
                visibleIndex += 1;
            }
            setHeight(rowMaxHeight);
            headerHeight = rowMaxHeight;
            if (Config.HeaderBorderWidth > 0)
            {
                AddInternal(new Box
                {
                    Height = Config.HeaderBorderWidth,
                    RelativeSizeAxes = Axes.X,
                    Colour = Colour4.White,
                    Alpha = 0.3f,
                    Y = headerHeight
                });
                headerHeight += Config.HeaderBorderWidth;
            }
        }

        BodyContainer.Y = headerHeight;

        for (currentRow = 0; currentRow < Rows.Count; currentRow++)
        {
            var row = Rows[currentRow];
            rowMaxHeight = 0;
            var visibleIndex = 0;
            for (var col = 0; col < allColumns.Length; col++)
            {
                var column = allColumns[col];
                if (column.Hidden) continue;
                addCell(column.GetDrawable(Config, row), visibleIndex, col);
                visibleIndex += 1;
            }
            setHeight(rowMaxHeight);
            y += rowMaxHeight;
        }

        if (Config.MaxHeight is float maxHeight)
        {
            if (y > maxHeight)
            {
                var scrollContainer = new DrumScrollContainer
                {
                    Height = maxHeight - headerHeight,
                    Y = headerHeight,
                    RelativeSizeAxes = Axes.X
                };
                for (var row = 1; row < Content.GetLength(0); row++)
                {
                    for (var col = 0; col < VisibleColumns.Count; col++)
                    {
                        BodyContainer.Remove(Content[row, col], false);
                        scrollContainer.Add(Content[row, col]);
                    }
                }
                RemoveInternal(BodyContainer, true);
                AddInternal(BodyContainer = scrollContainer);
            }
        }
        columnWidths.Invalidate();

        CellHover = new()
        {
            RelativeSizeAxes = Axes.Both,
            Depth = 1
        };

        BodyContainer.Add(RowHover = new()
        {
            Alpha = 0,
            Colour = DrumColors.RowHighlight,
            RelativeSizeAxes = Axes.X,
            Depth = 1
        });
    }
}