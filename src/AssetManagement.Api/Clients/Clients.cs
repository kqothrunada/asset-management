using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace AssetManagement.Api.Clients
{
    public record AssetSummary(string AssetCode, string Name, string Status);
    public record ClientContext(string BaseUrl, string Token);

    public interface IAssetClient
    {
        string Protocol { get; }
        Task<AssetSummary> GetAssetAsync(int id, ClientContext ctx, CancellationToken ct);
    }

    internal static class Http
    {
        public static async Task<string> SendAsync(IHttpClientFactory f, HttpMethod method, string url, string token,
            string? body, string? contentType, CancellationToken ct, params (string, string)[] headers)
        {
            using var req = new HttpRequestMessage(method, url);
            req.Headers.Authorization = new("Bearer", token);
            if (body is not null) req.Content = new StringContent(body, Encoding.UTF8, contentType ?? "application/json");
            foreach (var (k, v) in headers) req.Headers.TryAddWithoutValidation(k, v);
            using var res = await f.CreateClient().SendAsync(req, ct);
            var text = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) throw new HttpRequestException($"HTTP {(int)res.StatusCode}: {text}");
            return text;
        }
    }

    public class RestAssetClient : IAssetClient
    {
        private readonly IHttpClientFactory _f;
        public RestAssetClient(IHttpClientFactory f) => _f = f;
        public string Protocol => "REST";

        public async Task<AssetSummary> GetAssetAsync(int id, ClientContext c, CancellationToken ct)
        {
            var json = await Http.SendAsync(_f, HttpMethod.Get, $"{c.BaseUrl}/api/assets/{id}", c.Token, null, null, ct);
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            return new(r.GetProperty("assetCode").GetString()!, r.GetProperty("name").GetString()!, r.GetProperty("status").GetString()!);
        }
    }

    public class JsonRpcAssetClient : IAssetClient
    {
        private readonly IHttpClientFactory _f;
        public JsonRpcAssetClient(IHttpClientFactory f) => _f = f;
        public string Protocol => "JSON-RPC";

        public async Task<AssetSummary> GetAssetAsync(int id, ClientContext c, CancellationToken ct)
        {
            var body = JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "asset.get", @params = new { id }, id = 1 });
            var json = await Http.SendAsync(_f, HttpMethod.Post, $"{c.BaseUrl}/rpc/json", c.Token, body, "application/json", ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var err))
                throw new InvalidOperationException(err.GetProperty("message").GetString());
            var r = doc.RootElement.GetProperty("result");
            return new(r.GetProperty("assetCode").GetString()!, r.GetProperty("name").GetString()!, r.GetProperty("status").GetString()!);
        }
    }

    public class XmlRpcAssetClient : IAssetClient
    {
        private readonly IHttpClientFactory _f;
        public XmlRpcAssetClient(IHttpClientFactory f) => _f = f;
        public string Protocol => "XML-RPC";

        public async Task<AssetSummary> GetAssetAsync(int id, ClientContext c, CancellationToken ct)
        {
            var body = $"<methodCall><methodName>asset.get</methodName><params><param><value><int>{id}</int></value></param></params></methodCall>";
            var xml = await Http.SendAsync(_f, HttpMethod.Post, $"{c.BaseUrl}/rpc/xml", c.Token, body, "text/xml", ct);
            var doc = XDocument.Parse(xml);
            if (doc.Root!.Element("fault") is { } fault)
                throw new InvalidOperationException(fault.Descendants("member").First(m => m.Element("name")!.Value == "faultString").Element("value")!.Value);
            var m = doc.Descendants("member").ToDictionary(x => x.Element("name")!.Value, x => x.Element("value")!.Value);
            return new(m["assetCode"], m["name"], m["status"]);
        }
    }

    public class SoapAssetClient : IAssetClient
    {
        private readonly IHttpClientFactory _f;
        public SoapAssetClient(IHttpClientFactory f) => _f = f;
        public string Protocol => "SOAP";

        public async Task<AssetSummary> GetAssetAsync(int id, ClientContext c, CancellationToken ct)
        {
            var envelope = $"""
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body><GetAsset xmlns="http://tempuri.org/"><id>{id}</id></GetAsset></soap:Body>
            </soap:Envelope>
            """;
            var xml = await Http.SendAsync(_f, HttpMethod.Post, $"{c.BaseUrl}/soap/assets", c.Token, envelope, "text/xml", ct,
                ("SOAPAction", "\"http://tempuri.org/IAssetSoapService/GetAsset\""));
            var doc = XDocument.Parse(xml);
            string Field(string n) => doc.Descendants().First(e => e.Name.LocalName == n).Value;
            if (doc.Descendants().Any(e => e.Name.LocalName == "Fault"))
                throw new InvalidOperationException(Field("faultstring"));
            return new(Field("AssetCode"), Field("Name"), Field("Status"));
        }
    }
}
