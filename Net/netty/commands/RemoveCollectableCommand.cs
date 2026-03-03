using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class RemoveCollectableCommand
    {
        public const short ID = -21530;

        public static byte[] write(string hash, bool collected)
        {
            var param1 = new ByteArray(ID);
            param1.writeUTF(hash);
            param1.writeBoolean(collected);
            return param1.ToByteArray();
        }
    }
}
