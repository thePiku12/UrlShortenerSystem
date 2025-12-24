namespace UrlShortenerSystem.Models
{
    public class ShortenResponse
    {
        public required string ShortCode { get; set; }
        public required string ShortUrl { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}