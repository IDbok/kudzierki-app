namespace Api.Models.Responses;

public record WorkDayAssignmentResponse(
    Guid Id,
    Guid EmployeeId,
    int PortionType,
    decimal Portion,
    bool? Worked,
    string? Note);
