using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestConditionTypeModule
    {
        public const short ID = 9520;

        public static byte[] write(uint type = 0)
        {
            var packet = new ByteArray(ID);
            packet.writeShort(unchecked((short)type));
            return packet.Message.ToArray();
        }
    }
}
