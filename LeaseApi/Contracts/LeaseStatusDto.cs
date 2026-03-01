namespace LeaseApi.Contracts;

public class LeaseStatusDto
{
    public string TitleNumber { get; init; }
    public string Status { get; init; }
    public string? Error { get; init; }
}