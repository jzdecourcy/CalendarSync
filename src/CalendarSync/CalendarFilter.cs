using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CalendarSync;

/// <summary>
/// Filters an iCalendar feed down to the events we care about by keeping only
/// the VEVENT blocks whose text matches one of the configured include patterns.
///
/// The original VEVENT text is preserved byte-for-byte (UID, VTIMEZONE references,
/// recurrence rules, X- properties) so the result is a faithful subset of the
/// source feed. The only optional rewrites are the calendar name in the prologue
/// and a redundant prefix trimmed from each event's SUMMARY.
/// </summary>
public static class CalendarFilter
{
    private static readonly Regex EventBlock =
        new("BEGIN:VEVENT.*?END:VEVENT", RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex LineFold =
        new("\r?\n[ \t]", RegexOptions.Compiled);

    private static readonly Regex SummaryLine =
        new("^SUMMARY:.*$", RegexOptions.Multiline | RegexOptions.Compiled);

    public sealed record SummaryReplacement(string From, string To);

    public sealed record Result(string Output, int Kept, int Dropped);

    public static Result Filter(
        string source,
        IReadOnlyList<string> includePatterns,
        string? calendarName,
        IReadOnlyList<SummaryReplacement>? summaryReplacements = null)
    {
        if (String.IsNullOrEmpty(source))
        {
            throw new ArgumentException("Source calendar is empty.", nameof(source));
        }

        MatchCollection blocks = EventBlock.Matches(source);

        if (blocks.Count == 0)
        {
            // No events to filter (e.g. an out-of-season feed) — pass the body through unchanged.
            return new Result(RewriteCalendarName(source, calendarName), 0, 0);
        }

        int firstStart = blocks[0].Index;

        Match last = blocks[blocks.Count - 1];

        int afterLast = last.Index + last.Length;

        string prologue = source.Substring(0, firstStart);

        string epilogue = source.Substring(afterLast);

        var kept = new List<string>();

        int dropped = 0;

        foreach (Match block in blocks)
        {
            if (Matches(block.Value, includePatterns))
            {
                kept.Add(RewriteSummary(block.Value, summaryReplacements));
            }
            else
            {
                dropped++;
            }
        }

        prologue = RewriteCalendarName(prologue, calendarName);

        string body = String.Join("\r\n", kept);

        string output = prologue + body + epilogue;

        return new Result(output, kept.Count, dropped);
    }

    private static bool Matches(string eventBlock, IReadOnlyList<string> includePatterns)
    {
        // Unfold ICS continuation lines so values split across lines are searchable.
        string unfolded = LineFold.Replace(eventBlock, String.Empty);

        foreach (string pattern in includePatterns)
        {
            if (unfolded.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string RewriteSummary(
        string eventBlock,
        IReadOnlyList<SummaryReplacement>? summaryReplacements)
    {
        if (summaryReplacements is null || summaryReplacements.Count == 0)
        {
            return eventBlock;
        }

        // Only touch the SUMMARY line so replacement text can never bleed into
        // DESCRIPTION, LOCATION, or other fields.
        return SummaryLine.Replace(eventBlock, match =>
        {
            string line = match.Value;

            foreach (SummaryReplacement replacement in summaryReplacements)
            {
                line = line.Replace(replacement.From, replacement.To);
            }

            return line;
        });
    }

    private static string RewriteCalendarName(string text, string? calendarName)
    {
        if (String.IsNullOrEmpty(calendarName))
        {
            return text;
        }

        return Regex.Replace(
            text,
            "^X-WR-CALNAME:.*$",
            "X-WR-CALNAME:" + calendarName,
            RegexOptions.Multiline);
    }
}
