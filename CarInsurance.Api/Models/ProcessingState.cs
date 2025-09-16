namespace CarInsurance.Api.Models
{
    public class ProcessingState
    {
        public long Id { get; set; }
        public string Key { get; set; } = default!;
        public DateOnly Value { get; set; }
    }
}
