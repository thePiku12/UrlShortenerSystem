namespace UrlShortenerSystem.Models
{
    public class StatsResponse
    {
        public required string ShortCode { get; set; }
        public required string OriginalUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int HitCount { get; set; }
        public bool IsExpired { get; set; }
    }
}