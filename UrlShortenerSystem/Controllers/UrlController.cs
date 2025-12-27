using Microsoft.AspNetCore.Mvc;
using UrlShortenerSystem.Models;
using UrlShortenerSystem.Services;

namespace UrlShortenerSystem.Controllers
{
    [ApiController]
    [Route("")]
    public class UrlController(UrlService service) : ControllerBase
    {
        private readonly UrlService _service = service;

        [HttpPost("shorten")]
        public IActionResult Shorten([FromBody] ShortenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.OriginalUrl))
                return BadRequest("Original URL is required.");

            // In dev you'll still see localhost unless your host is msft.
            // In prod, if the request host is "msft", the returned ShortUrl will be https://msft/{code}
            var baseDomain = $"{Request.Scheme}://{Request.Host}";

            var result = _service.Shorten(request.OriginalUrl, baseDomain);
            return Ok(result);
        }

        [HttpGet("{shortCode}")]
        public IActionResult RedirectToOriginal(string shortCode)
        {
            var originalUrl = _service.Resolve(shortCode);
            if (originalUrl == null)
                return StatusCode(410, "Link expired or not found."); // 410 Gone

            //Controller helper that returns a 302 Found redirect by default.The framework sets the Location header to originalUrl.
            //Other variants: RedirectPermanent(301), RedirectPreserveMethod(307), RedirectPermanentPreserveMethod(308).
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