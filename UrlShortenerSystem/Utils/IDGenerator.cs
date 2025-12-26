namespace UrlShortenerSystem.Utils
{
    /// <summary>
    /// Simple sharded ID generator: 2-char machine/shard id (e.g. A0..Z9) + base62(sequence).
    /// </summary>
    public class IDGenerator
    {
        private readonly string _machineId; // e.g. "A0"
        private long _sequence = 0;
        //_lock is used to synchronize access to the code generation logic, making the increment of _sequence and code creation safe in multi-threaded scenarios.
        private readonly object _lock = new();

        public IDGenerator(string machineId) // requires if machineId coming from configs else update directly above at line 8
        {
            if (string.IsNullOrWhiteSpace(machineId) || machineId.Length != 2)
                throw new ArgumentException("Machine ID must be 2 characters in the form A0..Z9.", nameof(machineId));

            var letter = machineId[0];
            var digit = machineId[1];

            if (!(letter >= 'A' && letter <= 'Z') || !(digit >= '0' && digit <= '9'))
                throw new ArgumentException("Machine ID must be 2 characters in the form A0..Z9.", nameof(machineId));

            _machineId = machineId;
        }

        /// <summary>
        /// Returns a shortcode: {machineId}{base62 seq padded to totalLength - 2}.
        /// Note: sequence resets on process restart; for real durability, persist it or use a DB sequence.
        /// </summary>
        public string GenerateCode(int totalLength = 8)
        {
            if (totalLength < 3) // 2 chars machine id + at least 1 char payload
                throw new ArgumentOutOfRangeException(nameof(totalLength), "totalLength must be >= 3.");

            lock (_lock)
            {
                _sequence++;

                var base62Seq = Base62Encoder.Encode(_sequence);

                // totalLength includes the 2-char machineId
                var payloadLen = totalLength - 2;
                var padded = base62Seq.PadLeft(payloadLen, '0');

                // If sequence grows beyond payloadLen, you'll exceed the target length.
                return _machineId + padded;
            }
        }
    }
}