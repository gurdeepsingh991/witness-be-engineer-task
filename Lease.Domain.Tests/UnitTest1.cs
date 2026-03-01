using Lease.Domain.Models;
using Lease.Domain.Parsers;

namespace Lease.Domain.Tests;

public class LeaseParserTests
{
    private readonly LeaseParser _parser = new();

    [Fact]
    public void Parse_WithValidSingleEntry_ReturnsCorrectParsedData()
    {
        // Arrange
        var raw = new List<RawScheduleNoticeOfLease>
        {
            new()
            {
                EntryNumber = "1",
                EntryDate = "2024-01-01",
                EntryType = "Notice of Lease",
                EntryText = new List<string>
                {
                    "09.07.2009 Freehold title with covenants     01.06.1989 - 125 years  TGL24029"
                }
            }
        };

        // Act
        var result = _parser.Parse(raw).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(1, result[0].EntryNumber);
        Assert.NotNull(result[0].RegistrationDateAndPlanRef);
        Assert.NotNull(result[0].PropertyDescription);
        Assert.NotNull(result[0].DateOfLeaseAndTerm);
        Assert.Equal("TGL24029", result[0].LesseesTitle);
    }

    [Fact]
    public void Parse_WithMultipleLines_CombinesColumnsCorrectly()
    {
        // Arrange
        var raw = new List<RawScheduleNoticeOfLease>
        {
            new()
            {
                EntryNumber = "2",
                EntryDate = "2024-01-01",
                EntryType = "Notice of Lease",
                EntryText = new List<string>
                {
                    "09.07.2009 Part of freehold property known as  01.06.1989 - 125 years  TGL24029",
                    "         The Manor House"
                }
            }
        };

        // Act
        var result = _parser.Parse(raw).ToList();

        // Assert
        Assert.Single(result);
        Assert.Contains("Manor House", result[0].PropertyDescription);
    }

    [Fact]
    public void Parse_WithNotes_IncludesNotesInResult()
    {
        // Arrange
        var raw = new List<RawScheduleNoticeOfLease>
        {
            new()
            {
                EntryNumber = "3",
                EntryDate = "2024-01-01",
                EntryType = "Notice of Lease",
                EntryText = new List<string>
                {
                    "09.07.2009 Freehold property               01.06.1989 - 125 years  EGL557357",
                    "NOTE: This is an important note",
                    "NOTE: Additional information"
                }
            }
        };

        // Act
        var result = _parser.Parse(raw).ToList();

        // Assert
        Assert.Single(result);
        Assert.NotNull(result[0].Notes);
        Assert.Equal(2, result[0].Notes!.Count);
        Assert.Contains("NOTE: This is an important note", result[0].Notes);
    }

    [Fact]
    public void Parse_WithEmptyEntryText_ReturnsEmptyEntry()
    {
        // Arrange
        var raw = new List<RawScheduleNoticeOfLease>
        {
            new()
            {
                EntryNumber = "4",
                EntryDate = "2024-01-01",
                EntryType = "Notice of Lease",
                EntryText = new List<string>()
            }
        };

        // Act
        var result = _parser.Parse(raw).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(4, result[0].EntryNumber);
        Assert.Null(result[0].Notes);
    }

    [Fact]
    public void Parse_WithOnlyNotes_ReturnsEntryWithNotesOnly()
    {
        // Arrange
        var raw = new List<RawScheduleNoticeOfLease>
        {
            new()
            {
                EntryNumber = "5",
                EntryDate = "2024-01-01",
                EntryType = "Notice of Lease",
                EntryText = new List<string>
                {
                    "NOTE: Only notes present"
                }
            }
        };

        // Act
        var result = _parser.Parse(raw).ToList();

        // Assert
        Assert.Single(result);
        Assert.NotNull(result[0].Notes);
        Assert.Single(result[0].Notes!);
    }

    [Fact]
    public void Parse_WithMultipleEntries_ReturnsAllEntries()
    {
        // Arrange
        var raw = new List<RawScheduleNoticeOfLease>
        {
            new()
            {
                EntryNumber = "1",
                EntryDate = "2024-01-01",
                EntryType = "Notice of Lease",
                EntryText = new List<string>
                {
                    "09.07.2009 Property A                        01.06.1989 - 125 years  TGL24029"
                }
            },
            new()
            {
                EntryNumber = "2",
                EntryDate = "2024-01-02",
                EntryType = "Notice of Lease",
                EntryText = new List<string>
                {
                    "15.03.2010 Property B                        20.05.1990 - 99 years   EGL557357"
                }
            }
        };

        // Act
        var result = _parser.Parse(raw).ToList();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].EntryNumber);
        Assert.Equal(2, result[1].EntryNumber);
    }

    [Fact]
    public void Parse_WithVariousDateFormats_ParsesCorrectly()
    {
        // Arrange
        var raw = new List<RawScheduleNoticeOfLease>
        {
            new()
            {
                EntryNumber = "6",
                EntryDate = "2024-01-01",
                EntryType = "Notice of Lease",
                EntryText = new List<string>
                {
                    "01.01.1990 Property description               31.12.2020 - 50 years   ABC123"
                }
            }
        };

        // Act
        var result = _parser.Parse(raw).ToList();

        // Assert
        Assert.Single(result);
        Assert.NotNull(result[0].RegistrationDateAndPlanRef);
        Assert.NotNull(result[0].DateOfLeaseAndTerm);
    }

    [Fact]
    public void Parse_WithWhitespaceVariations_HandlesCorrectly()
    {
        // Arrange
        var raw = new List<RawScheduleNoticeOfLease>
        {
            new()
            {
                EntryNumber = "7",
                EntryDate = "2024-01-01",
                EntryType = "Notice of Lease",
                EntryText = new List<string>
                {
                    "09.07.2009   Property with extra spaces     01.06.1989 - 125 years  TGL24029"
                }
            }
        };

        // Act
        var result = _parser.Parse(raw).ToList();

        // Assert
        Assert.Single(result);
        // Should collapse multiple spaces
        Assert.DoesNotContain("  ", result[0].PropertyDescription);
    }

    [Fact]
    public void Parse_WithNoValidTitle_ReturnsEmptyTitle()
    {
        // Arrange
        var raw = new List<RawScheduleNoticeOfLease>
        {
            new()
            {
                EntryNumber = "8",
                EntryDate = "2024-01-01",
                EntryType = "Notice of Lease",
                EntryText = new List<string>
                {
                    "09.07.2009 Property description               01.06.1989 - 125 years  INVALID"
                }
            }
        };

        // Act
        var result = _parser.Parse(raw).ToList();

        // Assert
        Assert.Single(result);
        Assert.Empty(result[0].LesseesTitle);
    }

    [Fact]
    public void Parse_EntryShouldHaveNullEntryDate()
    {
        // Arrange
        var raw = new List<RawScheduleNoticeOfLease>
        {
            new()
            {
                EntryNumber = "9",
                EntryDate = "2024-01-01",
                EntryType = "Notice of Lease",
                EntryText = new List<string>
                {
                    "09.07.2009 Freehold property               01.06.1989 - 125 years  TGL24029"
                }
            }
        };

        // Act
        var result = _parser.Parse(raw).ToList();

        // Assert
        Assert.Null(result[0].EntryDate);
    }

    [Fact]
    public void Parse_WithComplexPropertyDescription_PreservesText()
    {
        // Arrange
        var raw = new List<RawScheduleNoticeOfLease>
        {
            new()
            {
                EntryNumber = "10",
                EntryDate = "2024-01-01",
                EntryType = "Notice of Lease",
                EntryText = new List<string>
                {
                    "09.07.2009 The Manor House, High Street, Town 01.06.1989 - 125 years  TGL24029",
                    "         including outbuildings and gardens"
                }
            }
        };

        // Act
        var result = _parser.Parse(raw).ToList();

        // Assert
        Assert.Single(result);
        var description = result[0].PropertyDescription;
        Assert.Contains("Manor House", description);
        Assert.Contains("High Street", description);
        Assert.Contains("outbuildings", description);
    }
}