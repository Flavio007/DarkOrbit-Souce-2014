using Ow.Game;
using Ow.Utils;
using System.Collections.Generic;
using System.Linq;

namespace Ow.Net.netty.commands
{
    class OreCountUpdateCommand
    {
        public const short ID = 11900;
        private const short WIRE_MARKER = -18182;

        public static byte[] write(IEnumerable<OreStackCommand> stacks)
        {
            var values = (stacks ?? Enumerable.Empty<OreStackCommand>()).ToList();
            var param1 = new ByteArray(ID);
            param1.writeShort(WIRE_MARKER);
            param1.writeInt(values.Count);
            foreach (var stack in values)
                param1.write(stack.write());
            return param1.ToByteArray();
        }

        public static byte[] write(int prometium, int endurium, int terbium, int xenomit,
            int prometid, int duranium, int promerium, int seprom, int palladium)
        {
            return write(new List<OreStackCommand>
            {
                OreStackCommand.FromServerOre(Ores.Prometium, prometium),
                OreStackCommand.FromServerOre(Ores.Endurium, endurium),
                OreStackCommand.FromServerOre(Ores.Terbium, terbium),
                OreStackCommand.FromServerOre(Ores.Xenomit, xenomit),
                OreStackCommand.FromServerOre(Ores.Prometid, prometid),
                OreStackCommand.FromServerOre(Ores.Duranium, duranium),
                OreStackCommand.FromServerOre(Ores.Promerium, promerium),
                OreStackCommand.FromServerOre(Ores.Seprom, seprom),
                OreStackCommand.FromServerOre(Ores.Palladium, palladium)
            });
        }
    }
}
