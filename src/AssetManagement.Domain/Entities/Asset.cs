namespace AssetManagement.Domain
{
    public class Asset
    {
        public int Id { get; set; }
        public string AssetCode { get; set; } = "";             // AST-001, read-only
        public string Name { get; set; } = "";
        public string? Description { get; set; }                // HTML dari rich text editor

        public string RequesterId { get; set; } = "";
        public string? ResponsiblePersonId { get; set; }

        public AssetCategory Category { get; set; }
        public AssetSubcategory Subcategory { get; set; }
        public AssetStatus Status { get; set; } = AssetStatus.New;

        public decimal AssetValueTotal { get; set; }            // terenkripsi di DB, dihitung dari maintenance

        // Untuk record-level permission
        public string CreatedById { get; set; } = "";
        public int CreatedByDivisionId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<AssetMaintenance> Maintenances { get; set; } = new();
        public List<ApprovalRequest> Approvals { get; set; } = new();
    }
}
