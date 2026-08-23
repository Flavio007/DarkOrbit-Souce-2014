using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestIconModule
    {
        public const short ID = 5610;

        public static byte[] write(short icon = 0)
        {
            var packet = new ByteArray(ID);
            packet.writeShort(icon);
            return packet.Message.ToArray();
        }
    }
}
