using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class AbilityStartCommand
    {
        public const short ID = 279;

        public static byte[] write(short selectedAbilityId, int activatorID, bool noStopCommand)
        {
            ByteArray param1 = new ByteArray(ID);
            param1.writeShort(selectedAbilityId);
            param1.writeInt(activatorID >> 13 | activatorID << 19);
            param1.writeBoolean(noStopCommand);
            return param1.ToByteArray();
        }
    }
}