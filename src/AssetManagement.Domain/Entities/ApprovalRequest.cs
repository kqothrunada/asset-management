namespace AssetManagement.Domain
{
    public class ApprovalRequest
    {
        public int Id { get; set; }
        public int AssetId { get; set; }
        public Asset Asset { get; set; } = null!;

        public string Role { get; set; } = "";                  // "Department Head" / "Asset Manager"
        public string ApproverId { get; set; } = "";
        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
        public string? Notes { get; set; }
        public DateTime? DecidedAt { get; set; }
    }
}
