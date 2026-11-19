using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameInterface.Configuration;

/// <summary>
/// Copies settings that are new in the shipped template into the text of an existing mod-config.json:
/// each top-level block, and each key inside a block, that the config lacks, with the template's comment
/// lines above it. A key the operator commented out counts as present. The edit is textual so operator
/// values, comments and ordering stay as they are.
/// </summary>
internal static class ModConfigTemplateMerger
{
    /// <summary>One "name": value member of a JSON object, as offsets into its text.</summary>
    private readonly struct JsonMember
    {
        public JsonMember(string name, int nameStart, int valueStart, int valueEnd, int end)
        {
            Name = name;
            NameStart = nameStart;
            ValueStart = valueStart;
            ValueEnd = valueEnd;
            End = end;
        }

        public string Name { get; }

        /// <summary>Index of the quote that opens the name.</summary>
        public int NameStart { get; }

        public int ValueStart { get; }

        /// <summary>Just past the value.</summary>
        public int ValueEnd { get; }

        /// <summary>Just past the comma after the value, or <see cref="ValueEnd"/> when there is none.</summary>
        public int End { get; }
    }

    /// <summary>
    /// Returns the config with the template's new settings added, and their names ("block" or "block.key").
    /// Both texts must parse as JSON.
    /// </summary>
    public static (string Merged, List<string> Added) Merge(string config, string template)
    {
        string newLine = config.Contains("\r\n") ? "\r\n" : "\n";
        var edits = new List<(int Offset, string Text)>();
        var added = new List<string>();

        var sharedBlocks = AddMissingMembers(config, SkipTrivia(config, 0), template, SkipTrivia(template, 0),
            "", newLine, edits, added);
        foreach (var (block, templateBlock) in sharedBlocks)
        {
            if (config[block.ValueStart] != '{' || template[templateBlock.ValueStart] != '{') continue;

            AddMissingMembers(config, block.ValueStart, template, templateBlock.ValueStart,
                templateBlock.Name + ".", newLine, edits, added);
        }

        string merged = config;
        foreach (var edit in edits.OrderByDescending(edit => edit.Offset))
        {
            merged = merged.Insert(edit.Offset, edit.Text);
        }
        return (merged, added);
    }

    /// <summary>Index of the bracket that closes the { or [ at open, or -1.</summary>
    private static int FindMatchingBrace(string text, int open)
    {
        int depth = 0;
        int i = open;
        while (i < text.Length)
        {
            char current = text[i];
            if (current == '"')
            {
                i = SkipString(text, i);
                if (i < 0) return -1;
                continue;
            }

            int afterTrivia = SkipTrivia(text, i);
            if (afterTrivia > i)
            {
                i = afterTrivia;
                continue;
            }

            if (current == '{' || current == '[') depth++;
            else if ((current == '}' || current == ']') && --depth == 0) return i;
            i++;
        }
        return -1;
    }

    // Queues an insertion for each template member the object at open lacks, placed after the previous template
    // member it has, or at the top of the object. Returns the members both objects have.
    private static List<(JsonMember Member, JsonMember TemplateMember)> AddMissingMembers(string text, int open,
        string template, int templateOpen, string prefix, string newLine, List<(int Offset, string Text)> edits,
        List<string> added)
    {
        var shared = new List<(JsonMember Member, JsonMember TemplateMember)>();
        var members = ReadMembers(text, open, out int close);
        var templateMembers = ReadMembers(template, templateOpen, out _);
        if (members == null || templateMembers == null) return shared;

        string block = text.Substring(open, close - open + 1);
        var pending = new List<string>();
        int previous = -1;
        foreach (var templateMember in templateMembers)
        {
            int index = members.FindIndex(member =>
                string.Equals(member.Name, templateMember.Name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                bool commentedOut = Regex.IsMatch(block,
                    "^[\\t ]*//[\\t ]*\\\"" + Regex.Escape(templateMember.Name) + "\\\"[\\t ]*:",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase);
                if (commentedOut) continue;

                int start = MemberStart(template, templateMember.NameStart);
                pending.Add(template.Substring(start, templateMember.ValueEnd - start)
                    .Replace("\r\n", "\n").Replace("\n", newLine));
                added.Add(prefix + templateMember.Name);
                continue;
            }

            if (pending.Count > 0) edits.AddRange(InsertAfter(text, members, previous, open, close, pending, newLine));
            pending.Clear();
            previous = index;
            shared.Add((members[index], templateMember));
        }
        if (pending.Count > 0) edits.AddRange(InsertAfter(text, members, previous, open, close, pending, newLine));

        return shared;
    }

    // Edits that insert the snippets after members[previous], or after the opening brace when previous is -1.
    private static List<(int Offset, string Text)> InsertAfter(string text, List<JsonMember> members, int previous,
        int open, int close, List<string> snippets, string newLine)
    {
        bool afterMember = previous >= 0;
        bool hasFollowing = previous + 1 < members.Count;
        int anchorEnd = afterMember ? members[previous].End : open + 1;
        int followingStart = hasFollowing ? members[previous + 1].NameStart : close;
        bool needsComma = afterMember && text[anchorEnd - 1] != ',';

        string joined = snippets[0];
        for (int i = 1; i < snippets.Count; i++)
        {
            joined += "," + newLine + (StartsWithComment(snippets[i]) ? newLine : "") + snippets[i];
        }
        if (hasFollowing) joined += ",";

        int lineEnd = text.IndexOf('\n', anchorEnd);
        bool followingSharesLine = lineEnd < 0 || lineEnd > followingStart;
        if (followingSharesLine)
        {
            return new List<(int Offset, string Text)> { (anchorEnd, (needsComma ? "," : "") + newLine + joined) };
        }

        // Insert at the end of the line, after any comment trailing it, with commented settings set apart by blank lines.
        if (text[lineEnd - 1] == '\r') lineEnd--;
        int nextStart = text.IndexOf('\n', lineEnd) + 1;
        int nextEnd = text.IndexOf('\n', nextStart);
        string nextLine = text.Substring(nextStart, (nextEnd < 0 ? text.Length : nextEnd) - nextStart).Trim();
        bool blankBefore = afterMember && StartsWithComment(snippets[0]);
        bool blankAfter = hasFollowing && nextLine.Length > 0 &&
            (StartsWithComment(snippets[snippets.Count - 1]) || nextLine.StartsWith("//", StringComparison.Ordinal));
        string insertion = newLine + (blankBefore ? newLine : "") + joined + (blankAfter ? newLine : "");

        // The missing comma goes straight after the value: in the insertion when the line ends there,
        // otherwise as its own edit so it stays in front of a comment trailing the value.
        if (!needsComma) return new List<(int Offset, string Text)> { (lineEnd, insertion) };
        if (lineEnd == anchorEnd) return new List<(int Offset, string Text)> { (lineEnd, "," + insertion) };
        return new List<(int Offset, string Text)> { (anchorEnd, ","), (lineEnd, insertion) };
    }

    private static bool StartsWithComment(string snippet) => snippet.TrimStart().StartsWith("//", StringComparison.Ordinal);

    // Lists the members of the object whose { is at open, or null when it isn't plain quoted members.
    private static List<JsonMember> ReadMembers(string text, int open, out int close)
    {
        var members = new List<JsonMember>();
        close = -1;
        int i = SkipTrivia(text, open + 1);
        while (i < text.Length && text[i] == '"')
        {
            int nameStart = i;
            int nameEnd = SkipString(text, nameStart);
            if (nameEnd < 0) return null;
            int colon = SkipTrivia(text, nameEnd);
            if (colon >= text.Length || text[colon] != ':') return null;
            int valueStart = SkipTrivia(text, colon + 1);
            int valueEnd = SkipValue(text, valueStart);
            if (valueEnd < 0) return null;

            int end = valueEnd;
            i = SkipTrivia(text, valueEnd);
            if (i < text.Length && text[i] == ',')
            {
                end = i + 1;
                i = SkipTrivia(text, i + 1);
            }
            members.Add(new JsonMember(text.Substring(nameStart + 1, nameEnd - nameStart - 2), nameStart, valueStart, valueEnd, end));
        }
        if (i >= text.Length || text[i] != '}') return null;

        close = i;
        return members;
    }

    // Index just past the JSON value that starts at start, or -1 when there is none.
    private static int SkipValue(string text, int start)
    {
        if (start >= text.Length) return -1;

        char first = text[start];
        if (first == '{' || first == '[')
        {
            int closing = FindMatchingBrace(text, start);
            return closing < 0 ? -1 : closing + 1;
        }
        if (first == '"') return SkipString(text, start);

        int end = start;
        while (end < text.Length && ",}]/".IndexOf(text[end]) < 0 && !char.IsWhiteSpace(text[end])) end++;
        return end > start ? end : -1;
    }

    // Index just past the string whose opening quote is at start, or -1 when it isn't closed.
    private static int SkipString(string text, int start)
    {
        for (int i = start + 1; i < text.Length; i++)
        {
            if (text[i] == '\\') i++;
            else if (text[i] == '"') return i + 1;
        }
        return -1;
    }

    // Index of the next character that isn't whitespace or a comment.
    private static int SkipTrivia(string text, int i)
    {
        while (i < text.Length)
        {
            char next = i + 1 < text.Length ? text[i + 1] : '\0';
            if (char.IsWhiteSpace(text[i]))
            {
                i++;
            }
            else if (text[i] == '/' && next == '/')
            {
                int lineEnd = text.IndexOf('\n', i);
                i = lineEnd < 0 ? text.Length : lineEnd + 1;
            }
            else if (text[i] == '/' && next == '*')
            {
                int commentEnd = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = commentEnd < 0 ? text.Length : commentEnd + 2;
            }
            else
            {
                break;
            }
        }
        return i;
    }

    // Where a member's text begins: its line, moved up over the // comment lines directly above it,
    // or the member itself when something precedes it on its line.
    private static int MemberStart(string text, int nameStart)
    {
        int lineStart = text.LastIndexOf('\n', nameStart - 1) + 1;
        if (!string.IsNullOrWhiteSpace(text.Substring(lineStart, nameStart - lineStart))) return nameStart;

        while (lineStart > 1)
        {
            int previous = text.LastIndexOf('\n', lineStart - 2) + 1;
            if (!text.Substring(previous, lineStart - previous).TrimStart().StartsWith("//", StringComparison.Ordinal)) break;
            lineStart = previous;
        }
        return lineStart;
    }
}
