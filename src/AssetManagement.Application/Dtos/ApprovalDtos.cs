using AssetManagement.Domain;

namespace AssetManagement.Application.Dtos
{
    public record ApprovalDto(int Id, string Role, string ApproverId, string ApproverName,
        ApprovalStatus Status, string? Notes, DateTime? DecidedAt, bool CanDecide);
}
