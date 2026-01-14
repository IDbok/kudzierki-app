namespace Infrastructure.Entities;

public enum EmployeePosition
{
    Owner = 0,
    Administrator = 1,
    Staff = 2,
    Cleaner = 3
}

public class Employee
{
    public Guid Id { get; set; }

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    public EmployeePosition Position { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }
    
}
