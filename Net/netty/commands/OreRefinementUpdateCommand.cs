using Ow.Utils;
using System.Collections.Generic;
using System.Linq;

namespace Ow.Net.netty.commands
{
    class OreRefinementUpdateCommand
    {
        public const short ID = 12658;

        public static byte[] write(IEnumerable<OreRefinementEntryCommand> entries)
        {
            var values = (entries ?? Enumerable.Empty<OreRefinementEntryCommand>()).ToList();
            var param1 = new ByteArray(ID);
            param1.writeInt(values.Count);
            foreach (var entry in values)
                param1.write(entry.write());
            return param1.ToByteArray();
        }
    }
}
