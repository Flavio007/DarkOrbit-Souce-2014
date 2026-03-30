using Ow.Game;
using Ow.Game.Objects;
using Ow.Game.Objects.Stations;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Net.netty.requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Net.netty.handlers.BattleStationRequestHandlers
{
    class EmergencyRepairRequestHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            gameSession?.Player?.SendPacket("0|A|STD|Legacy battle station emergency repair is disabled for faction stations.");
        }

        public async void Repair(int seconds, Satellite satellite)
        {
            //You can start multiple emergency repairs right after one another, but the first emergency repair need to finish before you can start the next.

            var activatable = satellite.Type == StationModuleModule.HULL || satellite.Type == StationModuleModule.DEFLECTOR ? (Activatable)satellite.BattleStation : satellite;

            if (activatable != null && activatable.CurrentHitPoints < activatable.MaxHitPoints)
            {
                satellite.EmergencyRepairActive = true;
                activatable.AddVisualModifier(VisualModifierCommand.EMERGENCY_REPAIR, 0, "", 0, true);

                for (int i = seconds; i > 0; i--)
                {
                    activatable.Heal(7500);
                    await Task.Delay(1000);

                    if (i <= 1 || activatable.CurrentHitPoints >= activatable.MaxHitPoints)
                    {
                        satellite.EmergencyRepairActive = false;
                        activatable.RemoveVisualModifier(VisualModifierCommand.EMERGENCY_REPAIR);
                        break;
                    }
                }   
            }
        }
    }
}
