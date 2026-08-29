using System.Collections.Generic;
using Ow.Utils;

namespace Ow.Net.netty.commands
{
    class SectorControlBeaconUpdateCommand
    {
        public const short ID = 15267;

        public static byte[] write(int captureProgress, double lockTimer, IEnumerable<int> capturingFactions, int currentFaction, string sectorHash)
        {
            var factions = new List<int>(capturingFactions ?? new int[0]);
            var packet = new ByteArray(ID);

            packet.writeInt(captureProgress >> 6 | captureProgress << 26);
            packet.writeShort(12530);
            packet.writeDouble(lockTimer);
            packet.writeShort(-12248);
            packet.writeInt(factions.Count);

            foreach (var faction in factions)
                packet.write(new FactionModule((short)faction).write());

            packet.write(new FactionModule((short)currentFaction).write());
            packet.writeUTF(sectorHash);
            return packet.ToByteArray();
        }
    }
}
