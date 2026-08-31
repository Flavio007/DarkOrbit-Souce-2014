using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class QuestConditionStateModule
    {
        public const short ID = 23281;
        private const short MARKER = -2605;

        public static byte[] write(double currentValue = 0, bool active = false, bool completed = false)
        {
            var packet = new ByteArray(ID);
            packet.writeBoolean(active);
            packet.writeShort(MARKER);
            packet.writeDouble(currentValue);
            packet.writeBoolean(completed);
            return packet.Message.ToArray();
        }
    }
}
