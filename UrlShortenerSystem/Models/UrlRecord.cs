namespace UrlShortenerSystem.Models
{
    public class UrlRecord
    {
        public required string ShortCode { get; set; }
        public required string OriginalUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiryDate { get; set; }

        private int _hitCount;

        public int HitCount
        {
            get => _hitCount;
            set => _hitCount = value;
        }

        // Expose a ref to the backing field so Interlocked can work
        public ref int HitCountRef => ref _hitCount;
    }
}