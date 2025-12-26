using Microsoft.AspNetCore.Mvc;
using UrlShortenerSystem.Models;
using UrlShortenerSystem.Services;

namespace UrlShortenerSystem.Controllers
{
    [ApiController]
    [Route("url")]
    public class UrlController(UrlService service) : ControllerBase
    {
        private readonly UrlService _service = service;

        [HttpPost("shorten")]
        public IActionResult Shorten([FromBody] ShortenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.OriginalUrl))
                return BadRequest("Original URL is required.");

            var baseDomain = $"{Request.Scheme}://{Request.Host}";
            var result = _service.Shorten(request.OriginalUrl, baseDomain);
            return Ok(result);
        }

        [HttpGet("{shortCode}")]
        public IActionResult RedirectToOriginal(string shortCode, [FromQuery] bool noRedirect = false)
        {
            var originalUrl = _service.Resolve(shortCode);
            if (originalUrl == null)
                return StatusCode(410, "Link expired or not found."); // 410 Gone  

            if (noRedirect)
                return Ok(new { OriginalUrl = originalUrl });

            return Redirect(originalUrl);
        }

        [HttpGet("{shortCode}/stats")]
        public IActionResult GetStats(string shortCode)
        {
            var stats = _service.GetStats(shortCode);
            if (stats == null)
                return NotFound("Short code not found.");

            return Ok(stats);
        }
    }
}