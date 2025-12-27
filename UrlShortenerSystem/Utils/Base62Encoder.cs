using System;
using System.Text;

namespace UrlShortenerSystem.Utils
{
    public static class Base62Encoder
    {
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static string Encode(long value)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value, nameof(value));
            if (value == 0) return Alphabet[0].ToString();

            var sb = new StringBuilder();

            // Append digits from least-significant to most-significant.
            while (value > 0)
            {
                var remainder = (int)(value % 62);
                sb.Append(Alphabet[remainder]); // O(1) amortized
                value /= 62;
            }

            // Reverse once (cheaper than inserting at index 0 repeatedly).
            var chars = sb.ToString().ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }
    }
}