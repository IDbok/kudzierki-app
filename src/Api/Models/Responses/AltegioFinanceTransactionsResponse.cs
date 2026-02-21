namespace Api.Models.Responses;

public sealed record AltegioFinanceTransactionsResponse(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AltegioFinanceCashRegisterTotalResponse> CashRegisterTotals,
    int TransactionsCount,
    IReadOnlyList<AltegioFinanceTransactionItemResponse> Transactions);

public sealed record AltegioFinanceCashRegisterTotalResponse(
    int? CashRegisterId,
    string? CashRegisterTitle,
    decimal TotalAmount,
    int TransactionsCount);
