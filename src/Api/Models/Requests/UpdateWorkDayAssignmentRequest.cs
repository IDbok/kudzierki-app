using System.ComponentModel.DataAnnotations;

namespace Api.Models.Requests;

public record UpdateWorkDayAssignmentRequest
{
    [Range(typeof(decimal), "0", "1")]
    public decimal? Portion { get; init; }

    [Range(0, 3)]
    public int? PortionType { get; init; }

    [MaxLength(1024)]
    public string? Note { get; init; }

    public bool? Worked { get; init; }
}
