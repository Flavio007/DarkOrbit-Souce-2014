namespace Ow.Net.netty.commands
{
    class QuestUpdateCommand
    {
        public const short ID = 8537;

        public QuestNettyModule Quest { get; private set; }

        public QuestUpdateCommand(QuestNettyModule quest)
        {
            Quest = quest ?? QuestNettyModule.FromWire(QuestDefinitionModule.write(0, "", ""));
        }

        public byte[] write()
        {
            var packet = new Ow.Utils.ByteArray(ID);
            packet.write(Quest.ToWireBytes());
            packet.writeShort(-4395);
            return packet.ToByteArray();
        }
    }
}
