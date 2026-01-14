using System;

namespace Infrastructure.Entities.Schedule;

public enum ShiftPortionType
{
    FullDay = 0,
    HalfDay = 1,
    Custom = 3
}

public class WorkDayAssignment
{
    public Guid Id { get; set; }

    public Guid WorkDayId { get; set; }
    public WorkDay WorkDay { get; set; } = null!;

    public Guid EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;

    /// <summary>
    /// Type of shift portion assigned to the employee.
    /// </summary>
    public ShiftPortionType PortionType { get; set; }

    /// <summary>
    /// Portion of the day assigned to the employee (e.g., 0.5 for half-day).
    /// </summary>
    public decimal Portion { get; set; }

    public bool? Worked { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }

}
