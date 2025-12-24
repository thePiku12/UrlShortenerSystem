using System.Collections.Concurrent;
using System.Threading;
using UrlShortenerSystem.Models;

namespace UrlShortenerSystem.Data
{
    public class InMemoryDatabase
    {
        private readonly ConcurrentDictionary<string, UrlRecord> _store = new();
        private readonly ConcurrentDictionary<string, string> _urlToCode = new();

        public bool TryAdd(UrlRecord record)
        {
            // Only add if the code is unused (prevents collisions/overwrites)
            if (!_store.TryAdd(record.ShortCode, record))
                return false;

            _urlToCode[record.OriginalUrl] = record.ShortCode;
            return true;
        }

        public void UpsertUrlIndex(string originalUrl, string shortCode)
        {
            _urlToCode[originalUrl] = shortCode;
        }

        public UrlRecord? Get(string shortCode)
        {
            _store.TryGetValue(shortCode, out var record);
            return record;
        }

        public string? GetShortCodeByUrl(string originalUrl)
        {
            _urlToCode.TryGetValue(originalUrl, out var code);
            return code;
        }

        public bool IncrementHitCount(string shortCode, out UrlRecord? record)
        {
            record = Get(shortCode);
            if (record is null) return false;

            Interlocked.Increment(ref record.HitCountRef);
            return true;
        }
    }
}