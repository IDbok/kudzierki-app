namespace Api.Models.Responses;

public sealed record AltegioEmployeeScheduleResponse(
    int EmployeeId,
    DateOnly Date,
    IReadOnlyList<AltegioScheduleItemResponse> Records);
