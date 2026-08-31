using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestConditionWrapperModule
    {
        public const short ID = 12207;

        public static byte[] write(QuestNettyModule condition, QuestNettyModule restriction)
        {
            var packet = new ByteArray(ID);
            packet.write((condition ?? QuestNettyModule.FromWire(QuestConditionModule.write())).ToWireBytes());
            packet.write((restriction ?? QuestNettyModule.FromWire(QuestRestrictionModule.write())).ToWireBytes());
            return packet.Message.ToArray();
        }
    }
}
