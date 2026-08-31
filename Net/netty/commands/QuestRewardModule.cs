using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestRewardModule
    {
        public const short ID = 19105;

        public static byte[] write(string lootId = "", int amount = 0)
        {
            var packet = new ByteArray(ID);
            packet.writeInt(QuestCommandCodec.RotateRight(amount, 7));
            packet.writeUTF(lootId);
            return packet.Message.ToArray();
        }
    }
}
