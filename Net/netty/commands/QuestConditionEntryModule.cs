using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestConditionEntryModule
    {
        public const short ID = 25570;

        public static byte[] write(int value = 0, string text = "")
        {
            var packet = new ByteArray(ID);
            packet.writeInt(QuestCommandCodec.RotateRight(value, 4));
            packet.writeUTF(text);
            return packet.Message.ToArray();
        }
    }
}
