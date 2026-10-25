using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using System;
using System.Xml;
using GrimService.Config;
using GrimService.Features.Locale;
using GrimService.Filters;
using GrimService.Models.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrimService.Helpers
{
    [ApiController]
    [PDIClient]
    [Route("/ap/locale")]
    [Produces("application/xml")]
    public class LocaleController : ControllerBase
    {
        private readonly ILogger<LocaleController> logger;
        private readonly ClientLocaleService locale;

        public LocaleController(IOptions<GameServerOptions> options, ILogger<LocaleController> logger,
            ClientLocaleService locale = null)
        {
            this.logger = logger;
            this.locale = locale ?? new ClientLocaleService(TimeProvider.System);
        }

        [HttpPost]
        public async Task<ActionResult> Post()
        {
            GrimRequest request;
            try
            {
                request = await GrimRequest.Deserialize(Request.Body, HttpContext.RequestAborted);
            }
            catch (Exception exception) when (exception is InvalidOperationException or XmlException)
            {
                logger.LogWarning("Invalid locale XML request");
                return BadRequest();
            }
            if (request == null) return BadRequest();

            string language = request.TryGetParameterByKey("language", out var parameter) ? parameter.Text : null;
            var reply = locale.Execute(request.Command, language);
            switch (reply.Status)
            {
                case LocaleStatus.Success:
                    if (request.Command == "servertime.get") logger.LogDebug("Got servertime.get request");
                    return Ok(GrimResult.FromString(reply.Value));
                case LocaleStatus.MissingLanguage:
                    logger.LogWarning("Got missing language parameter for language.set");
                    break;
                default:
                    logger.LogDebug("Received unimplemented locale command: {Command}", request.Command);
                    break;
            }
            return BadRequest();
        }
    }
}
