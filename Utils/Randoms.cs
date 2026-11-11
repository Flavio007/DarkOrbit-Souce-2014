using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Utils
{
    class Randoms
    {
        // Tick, network and async tutorial jobs share this generator. System.Random
        // is not thread safe on the .NET Framework/Mono used by the server.
        private sealed class SynchronizedRandom : Random
        {
            private readonly object gate = new object();
            public override int Next() { lock (gate) return base.Next(); }
            public override int Next(int maxValue) { lock (gate) return base.Next(maxValue); }
            public override int Next(int minValue, int maxValue) { lock (gate) return base.Next(minValue, maxValue); }
            public override double NextDouble() { lock (gate) return base.NextDouble(); }
            public override void NextBytes(byte[] buffer) { lock (gate) base.NextBytes(buffer); }
        }
        public static readonly Random random = new SynchronizedRandom();
        private static int nextObjectId = 1000000;
        private static readonly System.Security.Cryptography.RandomNumberGenerator hashRandom = System.Security.Cryptography.RandomNumberGenerator.Create();
        private static readonly object hashGate = new object();

        public static int CreateRandomID()
        {
            // Never reuse an old entity id during this server's lifetime. The old
            // recursive 99,998-id pool eventually exhausted and retained every id.
            int id = System.Threading.Interlocked.Increment(ref nextObjectId);
            if (id <= 0) throw new InvalidOperationException("Entity id space exhausted; restart the server.");
            return id;
        }

        public static string GenerateHash(int length)
        {
            if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
            const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            var result = new char[length];
            var bytes = new byte[32];
            int written = 0;
            lock (hashGate)
            {
                while (written < length)
                {
                    hashRandom.GetBytes(bytes);
                    foreach (byte value in bytes)
                    {
                        if (value >= 248) continue; // Unbiased base-62 characters.
                        result[written++] = chars[value % chars.Length];
                        if (written == length) break;
                    }
                }
            }
            return new string(result);
        }

        public static int GetRandomMap(int minid, int maxid, bool enablesecretmap) //manulaiko
        {
            if (enablesecretmap && random.Next(1, 1000) <= 2)
            {
                return 42;
            }
            else if (enablesecretmap && random.Next(1,10) <= 3)
            {
                return 58;
            }

            return random.Next(minid, maxid);
        }
    }
}
