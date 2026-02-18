using Ow.Game;
using Ow.Game.Objects;
using Ow.Net.netty.commands;
using Ow.Net.netty.requests;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Net.netty.handlers
{
    class PortalJumpRequestHandler : IHandler
    {
        private int GetInvasionTierByMapId(int mapId)
        {
            if (mapId >= 61 && mapId <= 63) return 1;
            if (mapId >= 64 && mapId <= 66) return 2;
            if (mapId >= 67 && mapId <= 69) return 3;
            return 0;
        }

        private bool CanEnterInvasionTier(int level, int tier)
        {
            if (tier == 1) return level >= 5 && level <= 9;
            if (tier == 2) return level >= 10 && level <= 14;
            if (tier == 3) return level >= 15;
            return true;
        }

        public void execute(GameSession gameSession, byte[] bytes)
        {
            var player = gameSession.Player;

            var spacemap = player.Spacemap;
            var activatable = player.Spacemap.GetActivatableMapEntity(player.CurrentInRangePortalId);

            if (activatable != null && activatable is Portal portal)
            {
                if (spacemap.Options.PvpMap)
                {
                    if(player.LastCombatTime.AddSeconds(10) > DateTime.Now)
                    {
                        string jumpError = "0|A|STM|jumpgate_failed_pvp_map";
                        player.SendPacket(jumpError);
                        return;
                    }
                }

                var invasionTier = GetInvasionTierByMapId(portal.TargetSpaceMapId);
                if (invasionTier > 0 && !CanEnterInvasionTier(player.Level, invasionTier))
                {
                    player.SendPacket("0|A|STD|You can't enter this Invasion Gate tier with your current level.");
                    return;
                }

                portal.Click(gameSession);
            }
            else
            {
                String warning = "0|A|STM|jumpgate_failed_no_gate";
                player.SendPacket(warning);
            }
        }
    }
}
