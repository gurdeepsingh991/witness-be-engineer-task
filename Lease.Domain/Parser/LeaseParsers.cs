using Lease.Domain.Models;
using System.Text.RegularExpressions;

namespace Lease.Domain.Parsers;

public class LeaseParser : ILeaseParser
{
    // Fallback column boundaries if auto-detection fails
    private const int DefaultCol2Start = 16;
    private const int DefaultCol3Start = 46;
    private const int DefaultCol4Start = 62;

    // Matches a date like 09.07.2009 or 01.06.1989 at the start of a line
    private static readonly Regex DatePattern = new(@"^\d{2}\.\d{2}\.\d{4}");

    // Matches a title reference like TGL24029, EGL557357
    private static readonly Regex TitlePattern = new(@"[A-Z]{2,3}\d+");

    public IEnumerable<ParsedScheduleNoticeOfLease> Parse(IEnumerable<RawScheduleNoticeOfLease> rawItems)
    {
        foreach (var raw in rawItems)
        {
            var notes = raw.EntryText
                .Where(x => x.TrimStart().StartsWith("NOTE"))
                .ToList();

            var contentLines = raw.EntryText
                .Where(x => !x.TrimStart().StartsWith("NOTE"))
                .ToList();

            if (!contentLines.Any())
            {
                yield return EmptyEntry(raw, notes);
                continue;
            }

            // --- Detect column boundaries from the first line ---
            // The first line always has all 4 columns populated and well-padded.
            // Col1: starts at 0
            // Col2: detected by finding where the property text starts (after the date + spaces)
            // Col3: detected by finding the second date in the line
            // Col4: detected by finding the title reference
            var (col2Start, col3Start, col4Start) = DetectColumnBoundaries(contentLines[0]);

            var col1 = new List<string>();
            var col2 = new List<string>();
            var col3 = new List<string>();
            var col4 = new List<string>();

            foreach (var line in contentLines)
            {
                col1.Add(SliceColumn(line, 0, col2Start));
                col2.Add(SliceColumn(line, col2Start, col3Start));
                col3.Add(SliceColumn(line, col3Start, col4Start));
                col4.Add(SliceColumn(line, col4Start, int.MaxValue));
            }

            yield return new ParsedScheduleNoticeOfLease
            {
                EntryNumber                = int.Parse(raw.EntryNumber),
                EntryDate                  = null,
                RegistrationDateAndPlanRef = JoinColumn(col1),
                PropertyDescription        = JoinColumn(col2),
                DateOfLeaseAndTerm         = JoinColumn(col3),
                LesseesTitle               = ExtractTitle(col4),
                Notes                      = notes.Any() ? notes : null
            };
        }
    }

    /// <summary>
    /// Detects col2/col3/col4 start positions from the first (fully-populated) line.
    /// Strategy:
    ///   - Col2 starts right after the opening date + its trailing whitespace
    ///   - Col3 is found by locating the second date in the line
    ///   - Col4 is found by locating the title reference (e.g. TGL/EGL + digits)
    /// Falls back to hardcoded defaults if detection fails.
    /// </summary>
    private static (int col2Start, int col3Start, int col4Start) DetectColumnBoundaries(string firstLine)
    {
        int col2 = DefaultCol2Start;
        int col3 = DefaultCol3Start;
        int col4 = DefaultCol4Start;

        // Col2: end of the opening date token (10 chars) + leading spaces
        var dateMatch = DatePattern.Match(firstLine);
        if (dateMatch.Success)
        {
            int afterDate = dateMatch.Index + dateMatch.Length;
            // Skip whitespace padding after the date to find where col2 begins
            while (afterDate < firstLine.Length && firstLine[afterDate] == ' ')
                afterDate++;
            col2 = afterDate;
        }

        // Col3: find the second date in the line (the lease date)
        var secondDateMatch = Regex.Match(firstLine.Substring(col2), @"\d{2}\.\d{2}\.\d{4}");
        if (secondDateMatch.Success)
        {
            int rawPos = col2 + secondDateMatch.Index;
            // Walk back to include any leading spaces that are part of the column padding
            // (column starts at the leftmost space before the date in that region)
            while (rawPos > col2 && firstLine[rawPos - 1] == ' ')
                rawPos--;
            col3 = rawPos > col2 ? rawPos : col2 + secondDateMatch.Index;
        }

        // Col4: find the title reference
        var titleMatch = TitlePattern.Match(firstLine);
        if (titleMatch.Success)
        {
            int rawPos = titleMatch.Index;
            while (rawPos > col3 && firstLine[rawPos - 1] == ' ')
                rawPos--;
            col4 = rawPos > col3 ? rawPos : titleMatch.Index;
        }

        return (col2, col3, col4);
    }

    /// <summary>
    /// Slices from [start] inclusive to [end] exclusive.
    /// Safe against lines shorter than start.
    /// </summary>
    private static string SliceColumn(string line, int start, int end)
    {
        if (start >= line.Length)
            return string.Empty;

        int safeEnd = end == int.MaxValue ? line.Length : Math.Min(end, line.Length);
        return line[start..safeEnd].Trim();
    }

    /// <summary>
    /// Joins non-empty column fragments and collapses internal whitespace.
    /// </summary>
    private static string JoinColumn(IEnumerable<string> parts)
    {
        var joined = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        return Regex.Replace(joined, @"\s{2,}", " ").Trim();
    }

    /// <summary>
    /// Extracts title reference from the first non-empty fragment of col4.
    /// The title is always on the first data row.
    /// </summary>
    private static string ExtractTitle(List<string> col)
    {
        var first = col.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? string.Empty;
        var m = TitlePattern.Match(first);
        return m.Success ? m.Value : string.Empty;
    }

    private static ParsedScheduleNoticeOfLease EmptyEntry(RawScheduleNoticeOfLease raw, List<string> notes) =>
        new()
        {
            EntryNumber = int.Parse(raw.EntryNumber),
            EntryDate   = null,
            Notes       = notes.Any() ? notes : null
        };
}