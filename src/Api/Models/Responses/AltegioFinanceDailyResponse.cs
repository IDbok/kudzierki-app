namespace Api.Models.Responses;

public sealed record AltegioFinanceDailyResponse(
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
