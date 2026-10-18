using AssetManagement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AssetManagement.Application.Dtos
{
    public class AssetUpsertDto
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? RequesterId { get; set; }          
        public string? ResponsiblePersonId { get; set; }
        public AssetCategory Category { get; set; }
        public AssetSubcategory Subcategory { get; set; }
        public AssetStatus Status { get; set; } = AssetStatus.New;   
    }

    public class AssetListQuery
    {
        public string? Q { get; set; }
        public AssetCategory? Category { get; set; }
        public AssetStatus? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public record AssetListDto(
        int Id, string AssetCode, string Name,
        AssetCategory Category, AssetSubcategory Subcategory,
        AssetStatus Status, decimal AssetValueTotal);

    public record AssetDetailDto(
        int Id, string AssetCode, string Name, string? Description,
        string RequesterId, string? RequesterName,
        string? ResponsiblePersonId, string? ResponsiblePersonName,
        AssetCategory Category, AssetSubcategory Subcategory, AssetStatus Status,
        decimal AssetValueTotal, bool ShowMaintenance, bool RequesterReadOnly, bool CanEdit,          
        string CreatedById, DateTime CreatedAt);

    public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);
}
