namespace Api.Models.Responses;

public record CashRegisterClosingResponse(
    DateOnly Date,
    decimal CashBalanceFact,
    decimal TerminalIncomeFact,
    DateOnly? DayBefore,
    decimal CashBalanceDayBefore,
    decimal CashIncomeFact,
    decimal CashIncomeAltegio,
    decimal CashIncomeDifference,
    decimal TerminalIncomeAltegio,
    decimal TerminalIncomeDifference,
    decimal TransferIncomeAltegio,
    decimal CashSpendingAdmin,
    decimal InCashTransfer,
    decimal OutCashTransfer,
    bool IsCashConfirmed,
    bool IsTerminalConfirmed,
    string? Comment);
