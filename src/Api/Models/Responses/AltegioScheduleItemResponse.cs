namespace Api.Models.Responses;

public sealed record AltegioScheduleItemResponse(
    int RecordId,
    DateTime StartAt,
    bool IsVisited,
    bool IsDeleted,
    string? ClientName,
    decimal TotalCost,
    decimal TotalCostToPay,
    IReadOnlyList<string> Services);
