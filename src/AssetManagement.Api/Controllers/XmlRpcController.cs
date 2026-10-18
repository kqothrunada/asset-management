using AssetManagement.Application.Dtos;
using AssetManagement.Application.Exceptions;
using AssetManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AssetManagement.Api.Controllers
{
    [ApiController]
    [Route("rpc/xml")]
    [Authorize]
    public class XmlRpcController : ControllerBase
    {
        private readonly AssetService _svc;
        public XmlRpcController(AssetService svc) => _svc = svc;

        [HttpPost, Consumes("text/xml", "application/xml")]
        public async Task<ContentResult> Post(CancellationToken ct)
        {
            XDocument doc;
            try
            {
                var settings = new XmlReaderSettings { Async = true, DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using var reader = XmlReader.Create(Request.Body, settings);        
                doc = await XDocument.LoadAsync(reader, LoadOptions.None, ct);
            }
            catch (XmlException) { return pXml(pFaultResponse(-32700, "Parse error")); }

            var method = doc.Root?.Element("methodName")?.Value;
            var value = doc.Root?.Element("params")?.Elements("param").FirstOrDefault()?.Element("value");
            var argText = value?.Elements().FirstOrDefault()?.Value ?? value?.Value;

            try
            {
                switch (method)
                {
                    case "asset.get":
                        if (!int.TryParse(argText, out var id)) return pXml(pFaultResponse(-32602, "First parameter should be integer (id)"));
                        var a = await _svc.GetAsync(id, User, ct);
                        return pXml(pResponse(pStruct(("id", a.Id), ("assetCode", a.AssetCode), ("name", a.Name),
                            ("status", a.Status.ToString()), ("category", a.Category.ToString()),
                            ("subcategory", a.Subcategory.ToString()), ("assetValueTotal", a.AssetValueTotal))));
                    case "asset.list":
                        var page = await _svc.ListAsync(User, new AssetListQuery { PageSize = 100 }, ct);
                        var items = page.Items.Select(x => pStruct(("id", x.Id), ("assetCode", x.AssetCode),
                            ("name", x.Name), ("status", x.Status.ToString()), ("assetValueTotal", x.AssetValueTotal)));
                        return pXml(pResponse(new XElement("value", new XElement("array", new XElement("data", items)))));
                    default:
                        return pXml(pFaultResponse(-32601, "Method not found"));
                }
            }
            catch (NotFoundException e) { return pXml(pFaultResponse(404, e.Message)); }
            catch (ForbiddenException e) { return pXml(pFaultResponse(403, e.Message)); }
        }

        #region Private
        private ContentResult pXml(XElement root) => Content(root.ToString(SaveOptions.DisableFormatting), "text/xml", Encoding.UTF8);

        private static XElement pResponse(XElement value) => new("methodResponse", new XElement("params", new XElement("param", value)));
        private static XElement pFaultResponse(int code, string msg) =>
            new("methodResponse", new XElement("fault", pStruct(("faultCode", code), ("faultString", msg))));

        private static XElement pStruct(params (string Key, object? Val)[] members) =>
            new("value", new XElement("struct", members.Select(m => new XElement("member", new XElement("name", m.Key), pVal(m.Val)))));

        private static XElement pVal(object? v) => v switch
        {
            int i => new XElement("value", new XElement("int", i)),
            decimal d => new XElement("value", new XElement("double", d.ToString(CultureInfo.InvariantCulture))),
            _ => new XElement("value", new XElement("string", v?.ToString() ?? ""))
        };
        #endregion
    }
}
