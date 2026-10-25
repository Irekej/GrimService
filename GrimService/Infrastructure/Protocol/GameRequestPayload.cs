using System;
using System.Threading.Tasks;
using System.Xml;
using GrimService.Models.Xml;
using Microsoft.AspNetCore.Http;

namespace GrimService.Infrastructure.Protocol;

public static class GameRequestPayload
{
    public static async Task<GrimRequest> ReadAsync(HttpRequest request)
    {
        try { return await GrimRequest.Deserialize(request.Body, request.HttpContext.RequestAborted); }
        catch (InvalidOperationException) { return null; }
        catch (XmlException) { return null; }
    }

    public static GrimRequest ReadXml(string xml)
    {
        if (string.IsNullOrEmpty(xml)) return null;
        try { return GrimRequest.Deserialize(xml); }
        catch (InvalidOperationException) { return null; }
        catch (XmlException) { return null; }
    }
}
