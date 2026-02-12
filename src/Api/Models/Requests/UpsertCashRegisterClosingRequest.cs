using System.ComponentModel.DataAnnotations;

namespace Api.Models.Requests;

public record UpsertCashRegisterClosingRequest
{
    [Required]
    public decimal CashBalanceFact { get; init; }

    [Required]
    public decimal TerminalIncomeFact { get; init; }

    [MaxLength(2048)]
    public string? Comment { get; init; }

    public decimal? CashIncomeAltegio { get; init; }
    public decimal? TransferIncomeAltegio { get; init; }
    public decimal? TerminalIncomeAltegio { get; init; }
    public decimal? CashSpendingAdmin { get; init; }
    public decimal? InCashTransfer { get; init; }
    public decimal? OutCashTransfer { get; init; }
}
