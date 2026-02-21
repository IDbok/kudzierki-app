namespace Api.Models.Responses;

public sealed record AltegioFinanceTransactionItemResponse(
    long Id,
    DateTime DateTime,
    DateTime? CreatedAt,
    DateTime? LastChangeDate,
    decimal Amount,
    string? Comment,
    int? AccountId,
    string? AccountTitle,
    bool IsCash);
