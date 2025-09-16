namespace CarInsurance.Api.Models;

public class Car
{
    public long Id { get; set; }
    public string Vin { get; set; } = default!; // TODO: enforce unique constraint
    public string? Make { get; set; }
    public string? Model { get; set; }
    public int YearOfManufacture { get; set; }

    public long OwnerId { get; set; }
    public Owner Owner { get; set; } = default!;

    public ICollection<InsurancePolicy> Policies { get; set; } = new List<InsurancePolicy>();
}
