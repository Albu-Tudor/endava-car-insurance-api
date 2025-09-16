namespace CarInsurance.Api.Models;

public class Owner
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Email { get; set; }

    public ICollection<Car> Cars { get; set; } = new List<Car>();
}
