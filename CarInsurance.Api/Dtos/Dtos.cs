using System.Text.Json.Serialization;

namespace CarInsurance.Api.Dtos;

public record CarDto(long Id, string Vin, string? Make, string? Model, int Year, long OwnerId, string OwnerName, string? OwnerEmail);
public record InsuranceValidityResponse(long CarId, string Date, bool Valid);

public record CreateClaimRequest(string ClaimDate, string Description, decimal Amount);
public record ClaimDto(long Id, long CarId, string ClaimDate, string Description, decimal Amount);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HistoryItemType
{
    Policy,
    Claim
}
public record CarHistoryItem(HistoryItemType Type,string StartDate, string? EndDate, string? Provider, string? Description, decimal? Amount);
public record CarHistoryResponse(long CarId, IReadOnlyList<CarHistoryItem> Items);
