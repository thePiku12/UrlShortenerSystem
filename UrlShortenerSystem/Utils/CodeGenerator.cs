//using System.Security.Cryptography;
//using System.Text;

//namespace UrlShortenerSystem.Utils
//{
//    public static class CodeGenerator
//    {
//        public static string GenerateDeterministicCode(string inputUrl, int length = 8)
//        {
//            using var sha256 = SHA256.Create();
//            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(inputUrl));
//            var base62 = ToBase62(hashBytes);
//            return base62.Substring(0, length);
//        }

//        private static string ToBase62(byte[] bytes)
//        {
//            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
//            var sb = new StringBuilder();
//            foreach (var b in bytes)
//            {
//                sb.Append(chars[b % chars.Length]);
//            }
//            return sb.ToString();
//        }
//    }
//}