using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Text;
using System.Xml;
using GrimService.Filters;
using GrimService.Config;
using GrimService.Services;

namespace GrimService.Controllers
{
    [ApiController, Authorize, PDIClient]
    public class MuseumController : ControllerBase
    {
        private readonly ILogger<MuseumController> logger;
        private readonly string root;
        public MuseumController(IOptions<GameServerOptions> options, ILogger<MuseumController> logger)
        { root=options.Value.XmlResourcePath; this.logger=logger; }

        [HttpGet("/data2/museum/{server}/{region}/museum_{fileRegion}_{arg}.xml")]
        [HttpGet("/data/museum/{server}/{region}/museum_{fileRegion}_{arg}.xml")]
        public IActionResult GetMuseum(string server,string region,string fileRegion,string arg)
        {
            try
            {
                var xml=MuseumResources.Xml(root,server,region,fileRegion,arg);
                if(xml==null)
                {
                    logger.LogWarning("GT5 museum XML missing: server={Server}, region={Region}, language={Language}, resource={Resource}",server,region,fileRegion,arg);
                    return NotFound();
                }
                var bytes=Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"+xml);
                Response.Headers["Cache-Control"]="no-cache";
                logger.LogDebug("GT5 museum XML: resource={Resource}, language={Language}, bytes={Bytes}",arg,fileRegion,bytes.Length);
                return File(bytes,"application/xml; charset=utf-8");
            }
            catch(ArgumentException) { return BadRequest(); }
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is XmlException ||
                ex is System.Text.Json.JsonException || ex is FormatException || ex is InvalidOperationException || ex is System.Collections.Generic.KeyNotFoundException)
            {
                logger.LogError(ex,"GT5 museum resource could not be read");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }

        [HttpGet("/data/museum/{server}/{region}/museum_{fileRegion}_{imageId:int}.img")]
        [HttpGet("/data2/museum/{server}/{region}/museum_{fileRegion}_{imageId:int}.img")]
        public IActionResult GetMuseumImage(string server,string region,string fileRegion,int imageId)
        {
            try { MuseumResources.Validate(server,region,fileRegion,imageId.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
            catch(ArgumentException) { return BadRequest(); }
            return GetMuseumImage1(imageId);
        }

        [HttpGet("/data/museum/image/museum_{imageId:int}.img")]
        [HttpGet("/data2/museum/image/museum_{imageId:int}.img")]
        public IActionResult GetMuseumImage1(int imageId)
        {
            var path=MuseumResources.Image(root,imageId);
            if(path==null)
            {
                logger.LogWarning("GT5 museum image missing: id={ImageId}",imageId);
                return NotFound();
            }

            Response.Headers["X-Content-Type-Options"]="nosniff";
            logger.LogDebug("GT5 museum image: id={ImageId}, bytes={Bytes}",imageId,new FileInfo(path).Length);
            return PhysicalFile(path,"application/octet-stream",enableRangeProcessing:true);
        }
    }
}
