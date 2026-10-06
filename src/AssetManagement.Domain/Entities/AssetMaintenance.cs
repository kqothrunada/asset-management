namespace AssetManagement.Domain
{
    public class AssetMaintenance
    {
        public int Id { get; set; }
        public int AssetId { get; set; }
        public Asset Asset { get; set; } = null!;

        public decimal Cost { get; set; }
        public MaintenanceType Type { get; set; }
        public string? Comments { get; set; }
        public string? Vendor { get; set; }
        public DateOnly Date { get; set; }
    }
}
