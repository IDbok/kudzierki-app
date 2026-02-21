namespace Infrastructure.Services;

public interface IAltegioService
{
    Task<IReadOnlyList<AltegioScheduleItem>> GetEmployeeScheduleAsync(int employeeId, DateOnly date, CancellationToken cancellationToken = default);
    Task<AltegioSalaryPeriodResult> GetEmployeeSalaryAsync(int employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    Task<AltegioFinanceDailyResult> GetFinanceDailyAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<AltegioFinanceTransactionsResult> GetFinanceTransactionsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
}

public sealed record AltegioScheduleItem(
    int RecordId,
    DateTime StartAt,
    bool IsVisited,
    bool IsDeleted,
    string? ClientName,
    decimal TotalCost,
    decimal TotalCostToPay,
    IReadOnlyList<string> Services);

public sealed record AltegioSalaryDay(DateOnly Date, decimal Amount);

public sealed record AltegioSalaryPeriodResult(
    int EmployeeId,
    DateOnly From,
    DateOnly To,
    decimal TotalAmount,
    IReadOnlyList<AltegioSalaryDay> Days);

public sealed record AltegioFinanceDailyResult(
    DateOnly Date,
    decimal IncomeTotal,
    decimal ExpenseTotal,
    decimal CashIncome,
    decimal CashExpense,
    decimal TerminalIncome,
    decimal TerminalExpense,
    decimal TransferIncome,
    decimal TransferExpense,
    int TransactionsCount);

public sealed record AltegioFinanceTransaction(
    long Id,
    DateTime DateTime,
    DateTime CreatedAt,
    DateTime LastChangeDate,
    decimal Amount,
    string? Comment,
    int? AccountId,
    string? AccountTitle,
    bool IsCash);

public sealed record AltegioFinanceTransactionsResult(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AltegioFinanceTransaction> Transactions);
