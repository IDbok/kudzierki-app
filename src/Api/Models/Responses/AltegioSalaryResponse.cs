namespace Api.Models.Responses;

public sealed record AltegioSalaryResponse(
    int EmployeeId,
    DateOnly From,
    DateOnly To,
    decimal TotalAmount,
    IReadOnlyList<AltegioSalaryDayResponse> Days);
