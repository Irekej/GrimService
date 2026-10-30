using System;
using System.IO;
using System.Security.Cryptography;
using GrimService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GrimService.Controllers;

[AllowAnonymous]
[Route("api/sprx-updates")]
public sealed class SprxUpdatesController : ControllerBase
{
    private readonly SprxUpdateCatalog catalog;
    private readonly ILogger<SprxUpdatesController> logger;
    public SprxUpdatesController(SprxUpdateCatalog catalog, ILogger<SprxUpdatesController> logger) { this.catalog = catalog; this.logger = logger; }
    [HttpGet]
    public IActionResult Index() => Ok(new { protocol = 1, signing = "RSA-2048-PKCS1-SHA256", releases = catalog.Inventory() });

    [HttpGet("{game}/{region}/manifest")]
    public IActionResult Manifest(string game, string region)
    {
        if (!SprxUpdateCatalog.Supported(game, region)) return NotFound();
        try
        {
            var release = catalog.Read(game, region);
            if (release == null) return NotFound();
            Response.Headers.CacheControl = "no-store";
            return File(catalog.Manifest(release), "application/octet-stream");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or CryptographicException or ArgumentException)
        {
            logger.LogWarning(ex, "SPRX update manifest unavailable for {Game}/{Region}", game, region);
            return StatusCode(503);
        }
    }
    [HttpGet("{game}/{region}/file")]
    public IActionResult Download(string game, string region, [FromQuery] uint revision, [FromQuery] string sha256)
    {
        if (!SprxUpdateCatalog.Supported(game, region) || revision == 0 || sha256 == null || sha256.Length != 64) return NotFound();
        byte[] expected;
        try { expected = Convert.FromHexString(sha256); } catch (FormatException) { return BadRequest(); }
        try
        {
            var release = catalog.Read(game, region, revision);
            if (release == null) return NotFound();
            if (!CryptographicOperations.FixedTimeEquals(expected, release.Digest)) return Conflict();
            Response.Headers.CacheControl = "no-store";
            logger.LogInformation("SPRX update download: {Game}/{Region}, revision={Revision}, bytes={Bytes}, range={Range}",
                game, region, revision, release.Bytes.Length, Request.Headers.Range.ToString());
            // Native clients resume a transiently interrupted transfer with a
            // validated suffix, still pinned to the signed revision and digest.
            return File(release.Bytes, "application/octet-stream", release.FileName, enableRangeProcessing: true);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "SPRX update download unavailable for {Game}/{Region}", game, region);
            return StatusCode(503);
        }
    }
}
