using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using DrumGame.Game.Beatmaps.Loaders;
using DrumGame.Game.Channels;
using DrumGame.Game.Utils;
using Newtonsoft.Json.Linq;

namespace DrumGame.Game.Skinning;

public static class SkinPathUtil
{
    // this costs ~0.01ms, so it's safe to call each frame
    public static (object Instance, MemberInfo Member) FinalMember<T>(this Expression<Func<Skin, T>> pathExpression)
    {
        if (pathExpression.Body.NodeType == ExpressionType.Constant && (pathExpression.Body as ConstantExpression).Value == null)
            return (null, null);
        // cleaner way to do this is with recursion, but I'm not a fan

        // the order is backwards, so we store and reverse
        var expressions = new List<Expression>();
        var exp = pathExpression.Body;
        while (exp != null && exp.Type != typeof(Skin))
        {
            if (exp is MemberExpression me)
            {
                expressions.Add(me);
                exp = me.Expression;
            }
            else if (exp is MethodCallExpression mce)
            {
                expressions.Add(exp);
                exp = mce.Object;
            }
            else throw new Exception($"Unexpected expression type: {exp.GetType()}");
        }

        object instance = Util.Skin;
        for (var i = expressions.Count - 1; i >= 0; i--)
        {
            var e = expressions[i];
            // last part
            if (i == 0) return (instance, ((MemberExpression)e).Member);
            if (e is MemberExpression me)
            {
                var member = me.Member;
                if (member is PropertyInfo pi)
                    instance = pi.GetValue(instance);
                else if (member is FieldInfo fi)
                    instance = fi.GetValue(instance);
            }
            else if (e is MethodCallExpression mce)
                instance = mce.Method.Invoke(instance, [((ConstantExpression)mce.Arguments[0]).Value]);
        }
        throw new Exception($"Skin path expression failed to resolve: {pathExpression}");
    }
    public static T Get<T>(this Expression<Func<Skin, T>> pathExpression)
    {
        if (pathExpression.TryGet(out var o)) return o;
        throw new KeyNotFoundException(pathExpression.ToString());
    }

    public static string PathString<T>(this Expression<Func<Skin, T>> pathExpression)
    {
        var members = new List<string>();
        var exp = pathExpression.Body;
        while (exp != null && exp.Type != typeof(Skin))
        {
            if (exp is MemberExpression me)
            {
                var name = me.Member.Name;
                members.Add("." + char.ToLowerInvariant(name[0]) + name[1..]);
                exp = me.Expression;
            }
            else if (exp is MethodCallExpression mce)
            {
                var method = mce.Method;
                if (method.Name == "get_Item")
                {
                    var arg = ((ConstantExpression)mce.Arguments[0]).Value;
                    // I tried using a serializer here but it was messy because it uses double quotes but we need singles
                    // realistically there won't be many dictionary key types, so it's fine to hard-code the options
                    if (arg is DrumChannel dc)
                        members.Add($"['{BJsonNote.GetChannelString(dc)}']");
                    else throw new NotImplementedException();
                }
                else throw new NotImplementedException();
                exp = mce.Object;
            }
            else throw new Exception($"Unexpected expression type: {exp.GetType()}");
        }
        var o = new StringBuilder();
        for (var i = members.Count - 1; i >= 0; i--)
        {
            if (i == members.Count - 1 && members[i][0] == '.')
                o.Append(members[i][1..]);
            else
                o.Append(members[i]);
        }
        return o.ToString();
    }
    public static T GetOrDefault<T>(this Expression<Func<Skin, T>> pathExpression)
        => pathExpression.TryGet(out var o) ? o : default;
    public static bool TryGet<T>(this Expression<Func<Skin, T>> pathExpression, out T v)
    {
        var (instance, member) = pathExpression.FinalMember(); // if member is null, it will return null at the bottom
        if (member is PropertyInfo pi)
        {
            v = (T)pi.GetValue(instance);
            return true;
        }
        else if (member is FieldInfo fi)
        {
            v = (T)fi.GetValue(instance);
            return true;
        }
        v = default;
        return false;
    }

    public static void Set<T>(this Expression<Func<Skin, T>> pathExpression, T value)
    {
        var (instance, member) = pathExpression.FinalMember(); // if member is null, it will skip setting
        if (member is PropertyInfo pi)
            pi.SetValue(instance, value);
        else if (member is FieldInfo fi)
            fi.SetValue(instance, value);
    }
    public static void SetAndDirty<T>(this Expression<Func<Skin, T>> pathExpression, T value)
    {
        pathExpression.Set(value);
        pathExpression.Dirty();
    }
    public static void Dirty<T>(this Expression<Func<Skin, T>> pathExpression)
    {
        Util.Skin.AddDirtyPath(pathExpression.PathString());
    }

    public static string GetDescriptionFromExpression<T>(this Expression<Func<Skin, T>> pathExpression)
    {
        var member = ((MemberExpression)pathExpression.Body).Member;
        return Util.MarkupDescription(member);
    }


    // https://stackoverflow.com/questions/56427214/how-to-add-a-new-jproperty-to-a-json-based-on-path
    // this probably has issues with arrays
    // we could look for brackets in the path to fix this
    public static void ReplaceOrAdd(this JToken root, string path, JToken value)
    {
        var partStart = 0;
        var currentNode = root;
        void handlePart(string pathPart, bool last)
        {
            if (last && value == null)
            {
                ((JObject)currentNode).Remove(pathPart);
                return;
            }
            var partNode = currentNode.SelectToken(pathPart);

            if (partNode == null)
            {
                if (last)
                {
                    if (value != null)
                        ((JObject)currentNode).Add(pathPart, value);
                    return;
                }
                else
                {
                    var newJ = new JObject();
                    ((JObject)currentNode).Add(pathPart, newJ);
                    currentNode = newJ;
                }
            }
            else
            {
                if (last)
                {
                    partNode.Replace(value);
                    return;
                }
                currentNode = partNode;
            }
        }
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] == '.')
            {
                handlePart(path[partStart..i], false);
                partStart = i + 1;
            }
            else if (path[i] == '[')
            {
                handlePart(path[partStart..i], false);
                partStart = i;
            }
        }
        handlePart(path[partStart..], true);
    }
}