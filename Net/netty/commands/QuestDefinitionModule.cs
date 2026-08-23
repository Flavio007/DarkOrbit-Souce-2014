using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestDefinitionModule
    {
        public const short ID = 10794;

        public static byte[] write(int questId, string title, string description)
        {
            var packet = new ByteArray(ID);
            packet.writeInt(QuestCommandCodec.RotateRight(questId, 15));
            packet.write(QuestRestrictionModule.write());
            packet.writeUTF(title);
            packet.writeInt(0);
            packet.writeUTF(description);
            packet.writeInt(0);
            packet.writeInt(0);
            return packet.Message.ToArray();
        }
    }
}
