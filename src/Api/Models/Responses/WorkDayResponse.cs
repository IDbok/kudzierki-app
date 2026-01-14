namespace Api.Models.Responses;

public record WorkDayResponse(
    DateOnly Date,
    IReadOnlyList<WorkDayAssignmentResponse> Assignments);
