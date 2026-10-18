using AssetManagement.Domain;

namespace AssetManagement.Application.Dtos
{
    public class MaintenanceUpsertDto
    {
        public decimal Cost { get; set; }
        public MaintenanceType Type { get; set; }
        public string? Comments { get; set; }
        public string? Vendor { get; set; }
        public DateOnly Date { get; set; }
    }

    public record MaintenanceDto(int Id, int AssetId, decimal Cost, MaintenanceType Type, string? Comments, string? Vendor, DateOnly Date);
    public record MaintenanceResult(MaintenanceDto? Maintenance, decimal AssetValueTotal);
}
