using System;
using System.Text;

namespace UrlShortenerSystem.Utils
{
    public static class Base62Encoder
    {
        private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public static string Encode(long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            if (value == 0) return Alphabet[0].ToString();

            var sb = new StringBuilder();
            while (value > 0)
            {
                var remainder = (int)(value % 62);
                sb.Insert(0, Alphabet[remainder]);
                value /= 62;
            }
            return sb.ToString();
        }
    }
}