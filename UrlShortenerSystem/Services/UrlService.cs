using System;
using UrlShortenerSystem.Data;
using UrlShortenerSystem.Models;
using UrlShortenerSystem.Utils;

namespace UrlShortenerSystem.Services
{
    public class UrlService(InMemoryDatabase db, IDGenerator idGenerator)
    {
        private readonly InMemoryDatabase _db = db;
        private readonly IDGenerator _idGenerator = idGenerator;
        //•	IDGenerator is a class defined elsewhere in your project.It is responsible for generating unique short codes.
        //•	_idGenerator is a private field in the UrlService class. It holds a reference to an instance(object) of the IDGenerator class.
        //•	idGenerator is a parameter passed to the UrlService constructor.

        public ShortenResponse Shorten(string originalUrl, string baseDomain)
        {
            // Optional idempotency: return existing mapping for the same long URL
            var existingCode = _db.GetShortCodeByUrl(originalUrl);
            if (!string.IsNullOrEmpty(existingCode))
            {
                var existingRecord = _db.Get(existingCode);
                if (existingRecord != null)
                {
                    return new ShortenResponse
                    {
                        ShortCode = existingCode,
                        ShortUrl = $"{baseDomain}/{existingCode}",
                        ExpiryDate = existingRecord.ExpiryDate
                    };
                }
            }

            // Generate a globally-unique-ish code (per machine) and handle collisions
            const int maxAttempts = 10;

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var code = _idGenerator.GenerateCode(totalLength: 8);

                var record = new UrlRecord
                {
                    ShortCode = code,
                    OriginalUrl = originalUrl,
                    CreatedAt = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow.AddYears(5)
                };

                if (_db.TryAdd(record))
                {
                    // ensure url index is stored (TryAdd already does; keep explicit if you later change DB)
                    _db.UpsertUrlIndex(originalUrl, code);

                    return new ShortenResponse
                    {
                        ShortCode = code,
                        ShortUrl = $"{baseDomain}/{code}",
                        ExpiryDate = record.ExpiryDate
                    };
                }
            }

            throw new InvalidOperationException("Failed to generate a unique short code. Please retry.");
        }

        public string? Resolve(string shortCode)
        {
            var record = _db.Get(shortCode);
            if (record is null) return null;

            if (DateTime.UtcNow > record.ExpiryDate)
                return null;

            _db.IncrementHitCount(shortCode, out _);
            return record.OriginalUrl;
        }

        public StatsResponse? GetStats(string shortCode)
        {
            var record = _db.Get(shortCode);
            if (record is null) return null;

            return new StatsResponse
            {
                ShortCode = record.ShortCode,
                OriginalUrl = record.OriginalUrl,
                CreatedAt = record.CreatedAt,
                ExpiryDate = record.ExpiryDate,
                HitCount = record.HitCount,
                IsExpired = DateTime.UtcNow > record.ExpiryDate
            };
        }
    }
}