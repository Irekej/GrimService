using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace GrimService.Features.Resources;

public sealed class GreetingResources(string root)
{
    public async Task CopyAsync(HttpContext context, string server, string region, string image = null)
    {
        if (!Regex.IsMatch(server ?? "", @"\A[a-zA-Z0-9_-]{1,32}\z") ||
            !Regex.IsMatch(region ?? "", @"\A[a-zA-Z]{2}\z") ||
            (image != null && !Regex.IsMatch(image, @"\A[a-zA-Z0-9_-]{1,128}\z")))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        region = region.ToLowerInvariant();
        string PathFor(string language) => image == null
            ? ClientResourceRoutes.GreetingList(server, language)
            : ClientResourceRoutes.GreetingImage(server, language, image);
        string resource = PathFor(region);
        if (!File.Exists(Path.Combine(root, resource))) resource = PathFor("us");
        context.Response.ContentType = image == null ? "application/xml" : "application/octet-stream";
        await new GameResourceStore(root).CopyAsync(context, resource);
    }
}
