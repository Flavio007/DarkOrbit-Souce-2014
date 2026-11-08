using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ow.Net
{
    // TCP reads have no packet boundaries. The unsigned two-byte length
    // includes the command ID but excludes the length prefix itself.
    internal sealed class ClientPacketBuffer
    {
        private readonly List<byte> pending = new List<byte>();
        private static readonly byte[] Policy = Encoding.ASCII.GetBytes("<policy-file-request/>\0");

        public void Feed(byte[] data, int count, Action<byte[]> packet, Action policy)
        {
            if (count < 0 || count > data.Length) throw new ArgumentOutOfRangeException("count");
            for (var i = 0; i < count; i++) pending.Add(data[i]);
            var offset = 0;
            while (offset < pending.Count)
            {
                var remaining = pending.Count - offset;
                if (pending[offset] == (byte)'<')
                {
                    // A binary length can also begin with 0x3c; only treat
                    // '<p' as the Flash socket-policy handshake.
                    if (remaining < 2) break;
                    if (pending[offset + 1] == (byte)'p')
                    {
                        var matches = true;
                        for (var i = 0; i < Math.Min(remaining, Policy.Length); i++)
                            if (pending[offset + i] != Policy[i]) { matches = false; break; }
                        if (matches)
                        {
                            if (remaining < Policy.Length) break;
                            offset += Policy.Length; policy(); continue;
                        }
                    }
                }
                if (remaining < 2) break;
                var length = (pending[offset] << 8) | pending[offset + 1];
                if (length < 2) throw new InvalidDataException("Invalid game packet length");
                if (remaining < length + 2) break;
                var frame = pending.GetRange(offset, length + 2).ToArray();
                offset += length + 2;packet(frame);
            }
            if (offset != 0) pending.RemoveRange(0, offset);
        }
    }
}
