using Microsoft.AspNetCore.Builder;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GrimService.Filters;
using Microsoft.AspNetCore.Authorization;
using GrimService.Config;
using Microsoft.Extensions.Options;

namespace GrimService.Controllers
{
    [ApiController]
    public class DiscordPhotoController : ControllerBase
    {
        private readonly GameServerOptions _gsOptions;

        public DiscordPhotoController(IOptions<GameServerOptions> options)
        {
            _gsOptions = options.Value;
        }

        [HttpGet]
        [Route("/photo/public/images/{args}.jpg")]
        public async Task<ActionResult> GetPublicImages(string args)
        {
            string[] spl = args.Split('_');
            if (spl.Length != 2)
                return BadRequest();

            if (!long.TryParse(spl[0], out long photoId) || !int.TryParse(spl[1], out int type))
                return BadRequest();

            if (type == 0)
            {
                if (!System.IO.File.Exists($"{_gsOptions.XmlResourcePath}/photo/image/{photoId}_1.jpg"))
                    return NotFound();

                FileStream fs = new FileStream($"{_gsOptions.XmlResourcePath}/photo/image/{photoId}_1.jpg", FileMode.Open, FileAccess.Read);
                return File(fs, "image/jpeg");
            }

            if (type == 1)
            {
                if (!System.IO.File.Exists($"{_gsOptions.XmlResourcePath}/photo/image/{photoId}_1.jpg"))
                    return NotFound();

                FileStream fs = new FileStream($"{_gsOptions.XmlResourcePath}/photo/image/{photoId}_1.jpg", FileMode.Open, FileAccess.Read);
                return File(fs, "image/jpeg");
            }

            return NotFound();
        }
    }
}
