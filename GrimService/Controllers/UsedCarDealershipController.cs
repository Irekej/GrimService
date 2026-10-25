using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

using GrimService.Filters;
using GrimService.Config;
using GrimService.Utils;
using GrimService.Services;
using System.Text;
using System.Text.Json;

namespace GrimService.Controllers
{



    [ApiController]
    [Authorize]
    [PDIClient]
    [Route("/used_car/")]
    [Produces("application/xml")]
    public class UsedCarDealershipController : ControllerBase
    {
        private readonly WeeklyDealershipService _dealer;
        private readonly ILogger<UsedCarDealershipController> _logger;

        public UsedCarDealershipController(WeeklyDealershipService dealer, ILogger<UsedCarDealershipController> logger)
        {
            _dealer = dealer;
            _logger = logger;
        }

        [HttpGet]
        [Route("{server}/used_car_list.xml")]
        public async Task Get(string server)
        {


            Response.Headers["Cache-Control"] = "no-store";
            byte[] bytes;
            try
            {
                bytes=Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"+_dealer.GetCurrentXml());
            }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is JsonException || ex is InvalidOperationException)
            {
                _logger.LogError(ex,"GT5 weekly dealer generation failed: server={Server}",server);
                Response.StatusCode=StatusCodes.Status503ServiceUnavailable;
                return;
            }
            Response.ContentType = "application/xml; charset=utf-8";
            Response.ContentLength=bytes.Length;
            _logger.LogDebug("GT5 weekly dealer response: server={Server}, http=200, bytes={Bytes}",server,bytes.Length);
            await Response.Body.WriteAsync(bytes);
        }
    }
}
