using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class CameraLockToCoordinatesCommand
    {
        public const short ID = 13276;

        public static byte[] write(int x, int y, float duration)
        {
            var packet = new ByteArray(ID);

            // Client command _SafeCls_803 / ID 13276.
            packet.writeFloat(duration);
            packet.writeInt(unchecked((int)(((uint)x << 10) | ((uint)x >> 22))));
            packet.writeInt(unchecked((int)(((uint)y >> 3) | ((uint)y << 29))));
            packet.writeShort(25917);

            return packet.ToByteArray();
        }
    }
}
