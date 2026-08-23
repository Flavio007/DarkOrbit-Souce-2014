using Ow.Utils;
using System.Collections.Generic;
using System.Linq;

namespace Ow.Net.netty.commands
{
    class OreCargoUpdateCommand
    {
        public const short ID = 30352;
        private const short WIRE_MARKER = 13053;

        public static byte[] write(short contextType, IEnumerable<OreStackCommand> stacks)
        {
            var values = (stacks ?? Enumerable.Empty<OreStackCommand>()).ToList();
            var param1 = new ByteArray(ID);
            param1.write(new OreSyncContextModule(contextType).write());
            param1.writeShort(WIRE_MARKER);
            param1.writeInt(values.Count);
            foreach (var stack in values)
                param1.write(stack.write());
            return param1.ToByteArray();
        }
    }
}
