using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace EnterpriseUI.Editors.Interactivity;

public enum LabelMarkupMode { Plain, Markdown, Html }

/// <summary>A piece of text with uniform formatting; <see cref="LineBreak"/> runs carry no text.</summary>
public sealed record MarkupRun(string Text, bool Bold = false, bool Italic = false, bool Underline = false, bool Strike = false, bool Code = false, string? Color = null, string? Href = null, bool LineBreak = false);

/// <summary>Parser for the inline subset of Markdown and HTML that labels, rotator panels and chat bubbles render.</summary>
public static class LabelMarkup
{
    private static readonly Regex Tag = new(@"<(/?)([a-zA-Z]+)((?:\s+[^>]*?)?)\s*(/?)>", RegexOptions.Compiled);
    private static readonly Regex Attribute = new(@"([a-zA-Z\-]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.Compiled);
    private static readonly Regex LinkAt = new(@"\G\[([^\]]+)\]\(([^)\s]+)\)", RegexOptions.Compiled);
    private static readonly Regex StyleColor = new(@"(?<![-\w])color\s*:\s*([^;""']+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<MarkupRun> Parse(string? text, LabelMarkupMode mode)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<MarkupRun>();
        return mode switch
        {
            LabelMarkupMode.Html => ParseHtml(text),
            LabelMarkupMode.Markdown => ParseMarkdown(text),
            _ => SplitLines(text, new Style())
        };
    }

    /// <summary>Text without any formatting, as a screen reader should read it.</summary>
    public static string ToPlainText(IEnumerable<MarkupRun> runs)
    {
        var builder = new StringBuilder();
        foreach (var run in runs) builder.Append(run.LineBreak ? "\n" : run.Text);
        return builder.ToString();
    }

    private sealed class Style
    {
        public int Bold, Italic, Underline, Strike, Code;
        public Stack<string?> Colors = new();
        public string? Href;

        public MarkupRun Make(string text) => new(text, Bold > 0, Italic > 0, Underline > 0 || Href != null, Strike > 0, Code > 0, Colors.Count > 0 ? Colors.Peek() : null, Href);
    }

    private static List<MarkupRun> SplitLines(string text, Style style)
    {
        var runs = new List<MarkupRun>();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (i > 0) runs.Add(new MarkupRun(string.Empty, LineBreak: true));
            if (lines[i].Length > 0) runs.Add(style.Make(lines[i]));
        }

        return runs;
    }

    private static List<MarkupRun> ParseHtml(string html)
    {
        var runs = new List<MarkupRun>();
        var style = new Style();
        var stack = new Stack<(string Tag, string? Color, string? Href)>();
        var position = 0;
        foreach (Match match in Tag.Matches(html))
        {
            if (match.Index > position) AddText(runs, style, WebUtility.HtmlDecode(html[position..match.Index]));
            position = match.Index + match.Length;
            var closing = match.Groups[1].Value == "/";
            var name = match.Groups[2].Value.ToLowerInvariant();
            if (name == "br")
            {
                runs.Add(new MarkupRun(string.Empty, LineBreak: true));
                continue;
            }

            if (closing)
            {
                Pop(style, stack, name);
                if (name is "p" or "div" && runs.Count > 0 && !runs[^1].LineBreak) runs.Add(new MarkupRun(string.Empty, LineBreak: true));
                continue;
            }

            var attributes = Attribute.Matches(match.Groups[3].Value).ToDictionary(a => a.Groups[1].Value.ToLowerInvariant(),
                a => a.Groups[2].Success ? a.Groups[2].Value : a.Groups[3].Success ? a.Groups[3].Value : a.Groups[4].Value);
            string? color = null, href = null;
            switch (name)
            {
                case "b": case "strong": style.Bold++; break;
                case "i": case "em": style.Italic++; break;
                case "u": style.Underline++; break;
                case "s": case "strike": case "del": style.Strike++; break;
                case "code": style.Code++; break;
                case "a":
                    href = attributes.GetValueOrDefault("href");
                    style.Href = href;
                    break;
                case "span": case "font":
                    color = attributes.GetValueOrDefault("color") ?? (attributes.TryGetValue("style", out var css) && StyleColor.Match(css) is { Success: true } m ? m.Groups[1].Value.Trim() : null);
                    style.Colors.Push(color);
                    break;
                case "p": case "div":
                    if (runs.Count > 0 && !runs[^1].LineBreak) runs.Add(new MarkupRun(string.Empty, LineBreak: true));
                    break;
                default: continue;
            }

            if (match.Groups[4].Value != "/") stack.Push((name, color, href));
            else Pop(style, stack, name, push: (name, color, href));
        }

        if (position < html.Length) AddText(runs, style, WebUtility.HtmlDecode(html[position..]));
        return runs;
    }

    private static void Pop(Style style, Stack<(string Tag, string? Color, string? Href)> stack, string name, (string, string?, string?)? push = null)
    {
        if (push != null) stack.Push((push.Value.Item1, push.Value.Item2, push.Value.Item3));
        if (stack.Count == 0 || stack.All(s => s.Tag != name)) return;
        while (stack.Count > 0)
        {
            var top = stack.Pop();
            switch (top.Tag)
            {
                case "b": case "strong": style.Bold--; break;
                case "i": case "em": style.Italic--; break;
                case "u": style.Underline--; break;
                case "s": case "strike": case "del": style.Strike--; break;
                case "code": style.Code--; break;
                case "a": style.Href = null; break;
                case "span": case "font": style.Colors.Pop(); break;
            }

            if (top.Tag == name) break;
        }
    }

    private static void AddText(List<MarkupRun> runs, Style style, string text)
    {
        text = Regex.Replace(text.Replace("\r", string.Empty).Replace('\n', ' '), @"\s{2,}", " ");
        if (text.Length > 0) runs.Add(style.Make(text));
    }

    private static List<MarkupRun> ParseMarkdown(string text)
    {
        var runs = new List<MarkupRun>();
        var style = new Style();
        var buffer = new StringBuilder();
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        void Flush()
        {
            if (buffer.Length == 0) return;
            runs.Add(style.Make(buffer.ToString()));
            buffer.Clear();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (style.Code > 0 && c != '`')
            {
                buffer.Append(c);
                continue;
            }

            if (c == '\\' && i + 1 < text.Length && "\\`*~[]()".Contains(text[i + 1]))
            {
                buffer.Append(text[++i]);
            }
            else if (c == '\n')
            {
                Flush();
                runs.Add(new MarkupRun(string.Empty, LineBreak: true));
            }
            else if (c == '`')
            {
                Flush();
                style.Code = style.Code > 0 ? 0 : 1;
            }
            else if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
            {
                Flush();
                style.Bold = style.Bold > 0 ? 0 : 1;
                i++;
            }
            else if (c == '*')
            {
                Flush();
                style.Italic = style.Italic > 0 ? 0 : 1;
            }
            else if (c == '~' && i + 1 < text.Length && text[i + 1] == '~')
            {
                Flush();
                style.Strike = style.Strike > 0 ? 0 : 1;
                i++;
            }
            else if (c == '[' && LinkAt.Match(text, i) is { Success: true } link)
            {
                Flush();
                style.Href = link.Groups[2].Value;
                buffer.Append(link.Groups[1].Value);
                Flush();
                style.Href = null;
                i += link.Length - 1;
            }
            else
            {
                buffer.Append(c);
            }
        }

        Flush();
        return runs;
    }
}
