using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class AddOreCommand
    {
        public const short ID = 147;

        public static byte[] write(int x, int y, short oreType, string hash)
        {
            var param1 = new ByteArray(ID);
            param1.writeUTF(hash);
            param1.writeInt(y >> 9 | y << 23);
            param1.writeInt(x >> 4 | x << 28);
            param1.writeShort(oreType);
            return param1.ToByteArray();
        }
    }
}
