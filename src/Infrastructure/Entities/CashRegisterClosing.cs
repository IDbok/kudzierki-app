namespace Infrastructure.Entities;

public class CashRegisterClosing
{
    public Guid Id { get; set; }

    public DateOnly Date { get; set; }

    public decimal CashBalanceFact { get; set; }
    public decimal TerminalIncomeFact { get; set; }

    public DateOnly? DayBefore { get; set; }
    public decimal CashBalanceDayBefore { get; set; }

    public decimal CashIncomeAltegio { get; set; }
    public decimal TransferIncomeAltegio { get; set; }
    public decimal TerminalIncomeAltegio { get; set; }

    public decimal CashSpendingAdmin { get; set; }
    public decimal InCashTransfer { get; set; }
    public decimal OutCashTransfer { get; set; }

    public string? Comment { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
}
