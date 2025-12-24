using System;

namespace UrlShortenerSystem.Utils
{
    /// <summary>
    /// Simple sharded ID generator: 1-char machine/shard id + base62(sequence).
    /// </summary>
    public class IDGenerator
    {
        private readonly char _machineId;
        private long _sequence = 0;
        private readonly object _lock = new();

        public IDGenerator(char machineId)
        {
            if (!char.IsLetterOrDigit(machineId))
                throw new ArgumentException("Machine ID must be a single letter/digit (Base62 friendly).", nameof(machineId));

            _machineId = machineId;
        }

        /// <summary>
        /// Returns an 8-char shortcode: {machineId}{7 chars base62 seq padded}.
        /// Note: sequence resets on process restart; for real durability, persist it or use a DB sequence.
        /// </summary>
        public string GenerateCode(int totalLength = 8)
        {
            if (totalLength < 2) throw new ArgumentOutOfRangeException(nameof(totalLength));

            lock (_lock)
            {
                _sequence++;

                var base62Seq = Base62Encoder.Encode(_sequence);

                // totalLength includes the machineId char
                var payloadLen = totalLength - 1;
                var padded = base62Seq.PadLeft(payloadLen, '0');

                // If sequence grows beyond payloadLen, you'll exceed the target length.
                // For simplicity, we allow it (production would rotate length / add bits / etc).
                return _machineId + padded;
            }
        }
    }
}