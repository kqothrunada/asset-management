using AssetManagement.Application.Dtos;
using AssetManagement.Infrastructure.Services;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace AssetManagement.Api.Soap
{
    [DataContract]
    public class AssetSoapDto
    {
        [DataMember] public int Id { get; set; }
        [DataMember] public string AssetCode { get; set; } = "";
        [DataMember] public string Name { get; set; } = "";
        [DataMember] public string Status { get; set; } = "";
        [DataMember] public string Category { get; set; } = "";
        [DataMember] public string Subcategory { get; set; } = "";
        [DataMember] public decimal AssetValueTotal { get; set; }
    }

    [ServiceContract]
    public interface IAssetSoapService
    {
        [OperationContract] Task<AssetSoapDto> GetAsset(int id);
        [OperationContract] Task<List<AssetSoapDto>> ListAssets();
    }

    public class AssetSoapService : IAssetSoapService
    {
        private readonly AssetService _svc;
        private readonly IHttpContextAccessor _http;
        public AssetSoapService(AssetService svc, IHttpContextAccessor http) { _svc = svc; _http = http; }

        private System.Security.Claims.ClaimsPrincipal User =>
            _http.HttpContext?.User is { Identity.IsAuthenticated: true } u ? u
            : throw new FaultException("Unauthorized: include the header Authorization: Bearer <token>");

        public async Task<AssetSoapDto> GetAsset(int id)
        {
            var a = await _svc.GetAsync(id, User, default);
            return new AssetSoapDto
            {
                Id = a.Id,
                AssetCode = a.AssetCode,
                Name = a.Name,
                Status = a.Status.ToString(),
                Category = a.Category.ToString(),
                Subcategory = a.Subcategory.ToString(),
                AssetValueTotal = a.AssetValueTotal
            };
        }

        public async Task<List<AssetSoapDto>> ListAssets()
        {
            var page = await _svc.ListAsync(User, new AssetListQuery { PageSize = 100 }, default);
            return page.Items.Select(a => new AssetSoapDto
            {
                Id = a.Id,
                AssetCode = a.AssetCode,
                Name = a.Name,
                Status = a.Status.ToString(),
                Category = a.Category.ToString(),
                Subcategory = a.Subcategory.ToString(),
                AssetValueTotal = a.AssetValueTotal
            }).ToList();
        }
    }
}
