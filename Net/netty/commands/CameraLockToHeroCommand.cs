using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class CameraLockToHeroCommand
    {
        public const short ID = 6619;

        public static byte[] write()
        {
            var packet = new ByteArray(ID);

            // Client command _SafeCls_758 / ID 6619.
            packet.writeShort(25455);

            return packet.ToByteArray();
        }
    }
}
