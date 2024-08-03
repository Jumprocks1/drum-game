using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DrumGame.Game.Utils;

public struct FilterFieldInfo<T> where T : ISearchable<T>
{
    public string Name;
    public string MarkupDescription;
    public FilterFieldInfo(string name, string description = null)
    {
        Name = name;
        MarkupDescription = description;
    }
    public FilterAccessor Accessor;
    public static implicit operator FilterFieldInfo<T>(string name) => new(name, null);
    public FilterAccessor GetAccessor()
    {
        if (Name == null) return null;
        return Accessor ??= T.GetAccessor(this) ?? GetDefaultAccessor(Name);
    }
    public static FilterAccessor GetDefaultAccessor(string name)
    {
        var field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (field != null)
        {
            var parameter = Expression.Parameter(typeof(T));
            var exp = Expression.Lambda(Expression.Field(parameter, field), parameter);
            return new FilterAccessor(exp);
        }
        var prop = typeof(T).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        if (prop != null)
        {
            var parameter = Expression.Parameter(typeof(T));
            var exp = Expression.Lambda(Expression.Property(parameter, prop), parameter);
            return new FilterAccessor(exp);
        }
        return null;
    }
}

public interface ISearchable<T> where T : ISearchable<T>
{
    public static abstract FilterFieldInfo<T>[] Fields { get; }
    public static virtual void LoadField(FilterFieldInfo<T> field) { }
    // prefer field.GetAccessor where possible, this should only show 1 reference
    public static virtual FilterAccessor GetAccessor(FilterFieldInfo<T> field) => null;
    public string FilterString { get; }
    public static virtual IEnumerable<T> ApplyFilter(IEnumerable<T> exp, FilterOperator<T> op, FilterFieldInfo<T> field, string value)
        => ApplyFilterBase(exp, op, field, value);
    // can't call static base virtual methods, so we use this instead
    public static IEnumerable<T> ApplyFilterBase(IEnumerable<T> exp, FilterOperator<T> op, FilterFieldInfo<T> field, string value)
    {
        var accessor = field.GetAccessor();
        if (accessor == null) return Enumerable.Empty<T>();
        return op.Apply(exp, accessor, value);
    }
}

public class FilterAccessor
{
    public readonly Delegate Delegate;
    public bool Time;
    public bool TimeSpan;
    public FilterAccessor EnumStringAccessor;
    public Type ReturnType => Delegate.Method.ReturnType;
    public FilterAccessor(Delegate del)
    {
        Delegate = del;
    }
    public FilterAccessor(LambdaExpression exp) : this(exp.Compile()) { }
}
public abstract class FilterOperator<T> where T : ISearchable<T>
{
    public string Identifier;
    public abstract IEnumerable<T> Apply(IEnumerable<T> exp, FilterAccessor accessor, string value);
}
public static class GenericFilterer<T> where T : ISearchable<T>
{
    class SortOp : FilterOperator<T>
    {
        readonly bool desc;
        public override IEnumerable<T> Apply(IEnumerable<T> exp, FilterAccessor accessor, string _)
        {
            if (accessor.Delegate is Func<T, string> s)
                return desc ? exp.OrderByDescending(e => s(e), StringComparer.OrdinalIgnoreCase) : exp.OrderBy(e => s(e), StringComparer.OrdinalIgnoreCase);
            else if (accessor.Delegate is Func<T, long> l)
                return desc ? exp.OrderByDescending(l) : exp.OrderBy(l);
            else if (accessor.Delegate is Func<T, int> i)
                return desc ? exp.OrderByDescending(i) : exp.OrderBy(i);
            else if (accessor.Delegate is Func<T, double> d)
                return desc ? exp.OrderByDescending(d) : exp.OrderBy(d);
            else
            {
                var ret = accessor.ReturnType;
                if (ret.IsEnum)
                {
                    var del = accessor.Delegate;
                    return desc ? exp.OrderByDescending(e => (int)del.DynamicInvoke(e)) :
                        exp.OrderBy(e => (int)del.DynamicInvoke(e));
                }
            }
            return exp;
        }
        public SortOp(string identifier, bool desc) { Identifier = identifier; this.desc = desc; }
    }
    class EqualsOp : FilterOperator<T>
    {
        public bool Invert = false;
        public override IEnumerable<T> Apply(IEnumerable<T> exp, FilterAccessor accessor, string value)
        {
            if (accessor.Delegate is Func<T, string> s)
                return exp.Where(e => Invert ^ (s(e)?.ToLower()?.Contains(value) ?? false));
            else if (accessor.Delegate is Func<T, long> l)
            {
                if (long.TryParse(value, out var valueNumber))
                    return exp.Where(e => Invert ^ (l(e) == valueNumber));
            }
            else if (accessor.Delegate is Func<T, int> i)
            {
                if (int.TryParse(value, out var valueNumber))
                    return exp.Where(e => Invert ^ (i(e) == valueNumber));
            }
            else if (accessor.Delegate is Func<T, double> d)
            {
                if (accessor.TimeSpan)
                {
                    if (NumericOp.TryParseTimeSpan(value, out var span))
                    {
                        var ticks = span.Ticks;
                        return exp.Where(e => Invert ^ ((long)(d(e) * TimeSpan.TicksPerMillisecond) == ticks));
                    }
                }
                else
                {
                    if (double.TryParse(value, out var valueNumber))
                        return exp.Where(e => Invert ^ (d(e) == valueNumber));
                }
            }
            else if (accessor.Delegate is Func<T, bool> b)
            {
                var valueBool = !("false".StartsWith(value) || value == "0");
                return exp.Where(e => Invert ^ (b(e) == valueBool));
            }
            else
            {
                var ret = accessor.ReturnType;
                if (ret.IsEnum)
                {
                    var del = accessor.Delegate;
                    if (int.TryParse(value, out var valueNumber))
                        return exp.Where(e => Invert ^ ((int)del.DynamicInvoke(e) == valueNumber));
                    else
                    {
                        if (accessor.EnumStringAccessor != null)
                            return Apply(exp, accessor.EnumStringAccessor, value);
                        return exp.Where(e => Invert ^ (del.DynamicInvoke(e).ToString().Contains(value, StringComparison.OrdinalIgnoreCase)));
                    }
                }
            }
            return exp;
        }
        public EqualsOp() { Identifier = "="; }
    }
    class NotEqualsOp : EqualsOp
    {
        public NotEqualsOp() { Identifier = "!="; Invert = true; }
    }
    class NumericOp : FilterOperator<T>
    {
        public static bool TryParseTimeSpan(string value, out TimeSpan span)
        {
            var i = 0;
            while (i < value.Length - 1 && char.IsLetter(value, value.Length - i - 1)) i++;
            if (double.TryParse(value[0..^i], out var d))
            {
                var units = value[^i..];
                try
                {
                    // these can throw out of range exceptions, ex `w>99999999d`
                    if (units == "s")
                    {
                        span = TimeSpan.FromSeconds(d);
                        return true;
                    }
                    if (units == "m")
                    {
                        span = TimeSpan.FromMinutes(d);
                        return true;
                    }
                    if (units == "h")
                    {
                        span = TimeSpan.FromHours(d);
                        return true;
                    }
                    if (units == "d")
                    {
                        span = TimeSpan.FromDays(d);
                        return true;
                    }
                    if (units == "ms" || units == "")
                    {
                        span = TimeSpan.FromMilliseconds(d);
                        return true;
                    }
                }
                catch { }
            }
            span = default;
            return false;
        }
        public static long ParseLongTime(string value)
        {
            if (TryParseTimeSpan(value, out var span))
            {
                try
                {
                    // this can throw out of range exceptions, ex `w>99999999d`
                    return DateTimeOffset.UtcNow.Add(-span).UtcTicks;
                }
                catch { }
            }
            // this works very well. It defaults to the local timezone, but adding `z` at the end goes into UTC
            if (DateTimeOffset.TryParse(value, out var dto))
                return dto.UtcTicks;
            return 0;
        }
        readonly Func<long, long, bool> OperateLong;
        readonly Func<int, bool> OperateComparable;
        public override IEnumerable<T> Apply(IEnumerable<T> exp, FilterAccessor accessor, string value)
        {
            if (accessor.Delegate is Func<T, long> l)
            {
                if (accessor.Time)
                {
                    var t = ParseLongTime(value);
                    return exp.Where(e => OperateLong(l(e), t));
                }
                else if (long.TryParse(value, out var valueNumber))
                    return exp.Where(e => OperateLong(l(e), valueNumber));
            }
            else if (accessor.Delegate is Func<T, int> i)
            {
                if (long.TryParse(value, out var valueNumber))
                    return exp.Where(e => OperateLong(i(e), valueNumber));
            }
            else if (accessor.Delegate is Func<T, double> d)
            {
                if (accessor.TimeSpan)
                {
                    if (TryParseTimeSpan(value, out var span))
                    {
                        var ms = span.TotalMilliseconds;
                        return exp.Where(e => OperateComparable(d(e).CompareTo(ms)));
                    }
                }
                else
                {
                    if (double.TryParse(value, out var valueNumber))
                        return exp.Where(e => OperateComparable(d(e).CompareTo(valueNumber)));
                }
            }
            else if (accessor.Delegate is Func<T, DateTimeOffset?> dto)
            {
                var t = ParseLongTime(value);
                return exp.Where(e =>
                {
                    var v = dto(e);
                    if (v is DateTimeOffset dtoV)
                        return OperateLong(dtoV.UtcTicks, t);
                    return false;
                });
            }
            else if (accessor.Delegate is Func<T, string> st)
            {
                return exp.Where(e =>
                {
                    var v = st(e);
                    if (v == null) return false; // null never matches
                    return OperateComparable(StringComparer.OrdinalIgnoreCase.Compare(v, value));
                });
            }
            else if (accessor.ReturnType.IsEnum)
            {
                var del = accessor.Delegate;
                if (int.TryParse(value, out var valueNumber))
                    return exp.Where(e => OperateLong((int)del.DynamicInvoke(e), valueNumber));
            }
            return Enumerable.Empty<T>();
        }

        public NumericOp(string identifier, Func<long, long, bool> f, Func<int, bool> f2)
        {
            Identifier = identifier;
            OperateLong = f;
            OperateComparable = f2;
        }
    }
    static FilterOperator<T>[] Operators = [
        // order matters slightly (for > vs >=)
        new NotEqualsOp(),
        new EqualsOp(),
        new NumericOp(">=", (a,b) => a >= b, i => i >= 0),
        new NumericOp(">", (a,b) => a > b, i => i > 0),
        new NumericOp("<=", (a,b) => a <= b, i => i <= 0),
        new NumericOp("<", (a,b) => a < b, i => i < 0),
        new SortOp("^^", true),
        new SortOp("^", false),
    ];

    static FilterFieldInfo<T>[] Fields = T.Fields;

    public static FilterFieldInfo<T> LookupField(string field)
    {
        foreach (var s in Fields)
            if (field == s.Name) return s;
        foreach (var s in Fields)
            if (s.Name.StartsWith(field)) return s;
        foreach (var s in Fields)
            if (s.Name.Contains(field)) return s;
        return new FilterFieldInfo<T>(null);
    }

    // this used to be the old filter method before we added `|` splitting
    // this method was not changed at all
    // technically we could remove the ToLower in this method
    static IEnumerable<T> FilterNoOr(IEnumerable<T> maps, string filter)
    {
        var split = filter.ToLowerInvariant().Split((char[])null, options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var res = maps;
        foreach (var s in split)
        {
            var opIndex = int.MaxValue;
            FilterOperator<T> op = null;
            foreach (var o in Operators)
            {
                var index = s.IndexOf(o.Identifier);
                if (index >= 0 && index < opIndex)
                {
                    op = o;
                    opIndex = index;
                }
            }
            if (op == null)
            {
                res = res.Where(e => e.FilterString.Contains(s, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                var fieldName = s[..opIndex];
                var value = s[(opIndex + op.Identifier.Length)..];
                if (!string.IsNullOrWhiteSpace(value) || op is SortOp)
                {
                    var field = LookupField(fieldName);
                    T.LoadField(field);
                    res = T.ApplyFilter(res, op, field, value);
                }
            }
        }
        return res;
    }

    public static IEnumerable<T> Filter(IEnumerable<T> maps, string filter)
    {
        var orSplit = filter.ToLowerInvariant().Split('|', options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (orSplit.Length > 1)
        {
            var mapsList = maps.AsList(); // we don't want to reiterate any filters in the IEnumerable
            var res = new HashSet<T>();
            foreach (var split in orSplit)
            {
                foreach (var map in FilterNoOr(mapsList, split))
                    res.Add(map);
            }
            return res;
        }
        else if (orSplit.Length == 1)
            return FilterNoOr(maps, orSplit[0]);
        return maps;
    }
}