using System;

namespace Infrastructure.Entities.Schedule;

public class WorkDay
{
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }

    public List<WorkDayAssignment> Assignments { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}
