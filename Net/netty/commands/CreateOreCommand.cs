using Ow.Game;
using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class CreateOreCommand
    {
        public const short ID = 28690;
        private const short FIRST_TRAILING_MARKER = 24318;
        private const short SECOND_TRAILING_MARKER = 12343;

        public static byte[] write(string hash, int y, int x, Ores ore)
        {
            if (!OreResourceTypeModule.TryFromServerOre(ore, out var resource))
                resource = new OreResourceTypeModule(OreResourceTypeModule.PROMETIUM);

            var param1 = new ByteArray(ID);
            param1.writeUTF(hash);
            param1.writeInt(y >> 9 | y << 23);
            param1.writeInt(x >> 4 | x << 28);
            // The client p-code reads the nested module before both trailing markers.
            param1.write(resource.write());
            param1.writeShort(FIRST_TRAILING_MARKER);
            param1.writeShort(SECOND_TRAILING_MARKER);
            return param1.ToByteArray();
        }
    }
}
