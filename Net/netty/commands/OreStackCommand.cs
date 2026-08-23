using Ow.Game;
using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class OreStackCommand
    {
        public const short ID = 20293;
        private const short WIRE_MARKER = -10749;

        public double Count { get; }
        public OreResourceTypeModule Resource { get; }

        public OreStackCommand(double count, OreResourceTypeModule resource)
        {
            Count = count;
            Resource = resource ?? new OreResourceTypeModule(OreResourceTypeModule.PROMETIUM);
        }

        public static OreStackCommand FromServerOre(Ores ore, double count)
        {
            return OreResourceTypeModule.TryFromServerOre(ore, out var resource)
                ? new OreStackCommand(count, resource)
                : new OreStackCommand(0, new OreResourceTypeModule(OreResourceTypeModule.PROMETIUM));
        }

        public byte[] write()
        {
            var param1 = new ByteArray(ID);
            param1.writeDouble(Count);
            param1.writeShort(WIRE_MARKER);
            param1.write(Resource.write());
            return param1.Message.ToArray();
        }

        public byte[] ToByteArray()
        {
            var param1 = new ByteArray(false);
            param1.write(write());
            return param1.ToByteArray();
        }

        public static OreStackCommand readNested(ByteParser parser)
        {
            parser.readShort();
            var count = parser.readDouble();
            parser.readShort();
            var resource = OreResourceTypeModule.readNested(parser);
            return new OreStackCommand(count, resource);
        }
    }
}
