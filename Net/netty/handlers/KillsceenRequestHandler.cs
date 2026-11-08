using Ow.Game;
using Ow.Game.Objects;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Net.netty.requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Net.netty.handlers
{
    class KillsceenRequestHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var read = new KillscreenRequest();
            read.readCommand(bytes);

            var player = gameSession.Player;

            lock(Ow.Game.Events.AuctionManager.Sync)
            lock(player)
            {
            if (!player.Destroyed) return;
            switch (read.selection.typeValue)
            {
                case KillScreenOptionTypeModule.BASIC_REPAIR:
                    player.Respawn(true);
                    break;
                case KillScreenOptionTypeModule.AT_DEATHLOCATION_REPAIR:
                    if (player.Data.uridium >= 300 && player.Spacemap.Options.DeathLocationRepair && player.Storage.KillscreenDeathLocationRepairTime.AddSeconds(30) <= DateTime.Now)
                    {
                        player.ChangeData(DataType.URIDIUM, 300, ChangeType.DECREASE);
                        player.Storage.KillscreenDeathLocationRepairTime = DateTime.Now;
                        player.Respawn(false, true, false);
                    }
                    break;
                case KillScreenOptionTypeModule.AT_JUMPGATE_REPAIR:
                    if (player.Data.uridium >= 200 && player.Storage.KillscreenPortalRepairTime.AddSeconds(15) <= DateTime.Now)
                    {
                        player.ChangeData(DataType.URIDIUM, 200, ChangeType.DECREASE);
                        player.Storage.KillscreenPortalRepairTime = DateTime.Now;
                        player.Respawn(false, false, true);
                    }
                    break;
            }
            }
        }
    }
}
