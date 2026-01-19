namespace Api.Models.Responses;

public record EmployeeResponse(
    Guid Id,
    string FirstName,
    string LastName,
    int Position,
    bool IsActive);
