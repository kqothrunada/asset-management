namespace AssetManagement.Application.Abstractions
{
    public interface IAssetNumberGenerator
    {
        Task<string> NextAsync(CancellationToken ct = default);
    }
}
