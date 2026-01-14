using Infrastructure.Entities;
using Infrastructure.Entities.Schedule;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Abstractions;

namespace Infrastructure.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(ApplicationDbContext context, ITimeProvider timeProvider)
    {
        var passwordHasher = new PasswordHasher<User>();
        var now = timeProvider.UtcNow;

        var adminUser = await EnsureUserAsync(
            context,
            passwordHasher,
            email: "admin@local",
            role: UserRole.Admin,
            password: "Admin123!",
            now);

        await EnsureUserAsync(
            context,
            passwordHasher,
            email: "owner@local",
            role: UserRole.Owner,
            password: "Owner123!",
            now);

        var (adminKatya, adminNastya, cleanerTatyana) = await EnsureEmployeesAsync(context, adminUser.Id);

        await EnsureJanuary2026ScheduleAsync(
            context,
            timeProvider,
            createdByUserId: adminUser.Id,
            adminKatya,
            adminNastya,
            cleanerTatyana);
    }

    private static async Task<User> EnsureUserAsync(
        ApplicationDbContext context,
        PasswordHasher<User> passwordHasher,
        string email,
        UserRole role,
        string password,
        DateTimeOffset now)
    {
        var existing = await context.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (existing is not null)
            return existing;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Role = role,
            CreatedAt = now.UtcDateTime,
            UpdatedAt = now.UtcDateTime
        };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<(Employee adminKatya, Employee adminNastya, Employee cleanerTatyana)> EnsureEmployeesAsync(
        ApplicationDbContext context,
        Guid adminUserId)
    {
        static async Task<Employee> EnsureEmployeeAsync(
            ApplicationDbContext innerContext,
            string firstName,
            string lastName,
            EmployeePosition position,
            Guid? userId)
        {
            var existing = await innerContext.Employees
                .SingleOrDefaultAsync(e => e.FirstName == firstName && e.LastName == lastName && e.Position == position);

            if (existing is not null)
            {
                if (userId is not null && existing.UserId is null)
                {
                    existing.UserId = userId;
                    await innerContext.SaveChangesAsync();
                }

                return existing;
            }

            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                FirstName = firstName,
                LastName = lastName,
                Position = position,
                UserId = userId
            };

            innerContext.Employees.Add(employee);
            await innerContext.SaveChangesAsync();
            return employee;
        }

        var adminKatya = await EnsureEmployeeAsync(
            context,
            firstName: "Катя",
            lastName: "Администратор",
            position: EmployeePosition.Administrator,
            userId: adminUserId);

        var adminNastya = await EnsureEmployeeAsync(
            context,
            firstName: "Настя",
            lastName: "Администратор",
            position: EmployeePosition.Administrator,
            userId: null);

        var cleanerTatyana = await EnsureEmployeeAsync(
            context,
            firstName: "Татьяна",
            lastName: "Уборщица",
            position: EmployeePosition.Cleaner,
            userId: null);

        return (adminKatya, adminNastya, cleanerTatyana);
    }

    private static async Task EnsureJanuary2026ScheduleAsync(
        ApplicationDbContext context,
        ITimeProvider timeProvider,
        Guid createdByUserId,
        Employee adminKatya,
        Employee adminNastya,
        Employee cleanerTatyana)
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2026, 1, 31);

        var anyExistingInMonth = await context.WorkDays
            .AnyAsync(wd => wd.Date >= start && wd.Date <= end);
        if (anyExistingInMonth)
            return;

        var now = timeProvider.UtcNow;

        var random = new Random(202601);
        var splitShiftDays = new HashSet<DateOnly>();
        while (splitShiftDays.Count < 2)
        {
            var day = random.Next(1, 32);
            splitShiftDays.Add(new DateOnly(2026, 1, day));
        }

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var workDay = new WorkDay
            {
                Id = Guid.NewGuid(),
                Date = date,
                CreatedAt = now,
                CreatedByUserId = createdByUserId
            };

            var isCleaningDay = date.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Thursday;
            if (isCleaningDay)
            {
                workDay.Assignments.Add(new WorkDayAssignment
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = cleanerTatyana.Id,
                    PortionType = ShiftPortionType.FullDay,
                    Portion = 1.0m,
                    Note = "Уборка",
                    CreatedAt = now,
                    CreatedByUserId = createdByUserId
                });
            }

            if (splitShiftDays.Contains(date))
            {
                workDay.Assignments.Add(new WorkDayAssignment
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = adminKatya.Id,
                    PortionType = ShiftPortionType.HalfDay,
                    Portion = 0.5m,
                    Note = "Админ (первая половина)",
                    CreatedAt = now,
                    CreatedByUserId = createdByUserId
                });

                workDay.Assignments.Add(new WorkDayAssignment
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = adminNastya.Id,
                    PortionType = ShiftPortionType.HalfDay,
                    Portion = 0.5m,
                    Note = "Админ (вторая половина)",
                    CreatedAt = now,
                    CreatedByUserId = createdByUserId
                });
            }
            else
            {
                var adminOfDay = (date.Day % 2 == 0) ? adminKatya : adminNastya;
                workDay.Assignments.Add(new WorkDayAssignment
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = adminOfDay.Id,
                    PortionType = ShiftPortionType.FullDay,
                    Portion = 1.0m,
                    Note = "Админ",
                    CreatedAt = now,
                    CreatedByUserId = createdByUserId
                });
            }

            context.WorkDays.Add(workDay);
        }

        await context.SaveChangesAsync();
    }
}
