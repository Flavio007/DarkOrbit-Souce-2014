using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class SectorControlBeaconProgressVisibilityCommand
    {
        public const short ID = 24512;

        public static byte[] write(string sectorHash, bool visible)
        {
            var packet = new ByteArray(ID);
            // Client read order: magic, visible flag, hash, trailing magic.
            packet.writeShort(22786);
            packet.writeBoolean(visible);
            packet.writeUTF(sectorHash);
            packet.writeShort(-8553);
            return packet.ToByteArray();
        }
    }
}
