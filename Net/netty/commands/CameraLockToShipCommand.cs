using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class CameraLockToShipCommand
    {
        public const short ID = 4585;

        public static byte[] write(int shipUserId, float zoomFactor, float duration)
        {
            var packet = new ByteArray(ID);

            // Client command _SafeCls_981 / ID 4585.
            packet.writeFloat(zoomFactor);
            packet.writeInt(unchecked((int)(((uint)shipUserId << 6) | ((uint)shipUserId >> 26))));
            packet.writeFloat(duration);

            return packet.ToByteArray();
        }
    }
}
