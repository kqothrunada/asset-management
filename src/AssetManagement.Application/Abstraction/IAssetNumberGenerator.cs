namespace AssetManagement.Application.Abstraction
{
    public interface IAssetNumberGenerator
    {
        Task<string> NextAsync(CancellationToken ct = default);
    }
}
