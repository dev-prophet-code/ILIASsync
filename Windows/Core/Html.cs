using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace ILIASSync.Core;

internal static class Html
{
    private const RegexOptions Opt = RegexOptions.IgnoreCase | RegexOptions.Singleline;

    public sealed record Anchor(string Href, string Text);
    public sealed record Field(string Name, string Value, string Type);

    public static string Decode(string s) => WebUtility.HtmlDecode(s);

    public static string StripTags(string s)
    {
        var t = Regex.Replace(s, "<[^>]+>", " ");
        return Regex.Replace(Decode(t), @"\s+", " ").Trim();
    }

    public static List<Anchor> Anchors(string html) =>
        Regex.Matches(html, "<a\\b[^>]*?href\\s*=\\s*\"([^\"]*)\"[^>]*>(.*?)</a>", Opt)
            .Select(m => new Anchor(Decode(m.Groups[1].Value), StripTags(m.Groups[2].Value)))
            .ToList();

    public static string? BaseHref(string html)
    {
        var m = Regex.Match(html, "<base\\b[^>]*?href\\s*=\\s*\"([^\"]*)\"", Opt);
        return m.Success ? Decode(m.Groups[1].Value) : null;
    }

    public static string? Attr(string name, string tag)
    {
        var m = Regex.Match(tag, "(?<![\\w-])" + name + "\\s*=\\s*\"([^\"]*)\"", Opt);
        return m.Success ? Decode(m.Groups[1].Value) : null;
    }

    /// <summary>Findet das Formular, das <paramref name="containing"/> enthält, und liefert Action und Felder.</summary>
    public static (string? Action, List<Field> Fields)? Form(string containing, string html)
    {
        foreach (Match f in Regex.Matches(html, "<form\\b([^>]*)>(.*?)</form>", Opt))
        {
            if (!f.Groups[2].Value.Contains(containing)) continue;
            var fields = new List<Field>();
            foreach (Match t in Regex.Matches(f.Groups[2].Value, "<(?:input|button)\\b[^>]*>", Opt))
            {
                var name = Attr("name", t.Value);
                if (name == null) continue;
                fields.Add(new Field(name, Attr("value", t.Value) ?? "", (Attr("type", t.Value) ?? "text").ToLowerInvariant()));
            }
            return (Attr("action", "<form " + f.Groups[1].Value + ">"), fields);
        }
        return null;
    }
}
