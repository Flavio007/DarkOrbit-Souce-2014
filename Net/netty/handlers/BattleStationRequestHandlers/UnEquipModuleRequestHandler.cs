using Ow.Game;
using Ow.Game.Objects.Stations;
using Ow.Managers;
using Ow.Net.netty.commands;
using Ow.Net.netty.requests.BattleStationRequests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Net.netty.handlers.BattleStationRequestHandlers
{
    class UnEquipModuleRequestHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var player = gameSession?.Player;
            if (player == null)
                return;

            var request = new UnEquipModuleRequest();
            request.readCommand(bytes);

            var battleStation = player.Spacemap?.GetActivatableMapEntity(request.battleStationId) as BattleStation;
            if (battleStation == null || !battleStation.IsClanBattleStation)
            {
                player.SendPacket("0|A|STD|Faction battle station modules permanecem no sistema atual.");
                return;
            }

            if (!battleStation.CanManage(player))
            {
                player.SendPacket("0|A|STD|Apenas o lider do cla pode gerenciar os modulos da estacao por enquanto.");
                player.SendCommand(BattleStationErrorCommand.write(BattleStationErrorCommand.NO_CLAN));
                return;
            }

            var satellite = battleStation.GetSatelliteByItemId(request.itemId);
            if (satellite == null)
            {
                player.SendCommand(BattleStationErrorCommand.write(BattleStationErrorCommand.ITEM_NOT_IN_STATION));
                return;
            }

            satellite.Remove(false, true, false);

            if (battleStation.ShouldDisplayModuleAsSatellite(satellite.SlotId))
            {
                battleStation.Spacemap.Activatables.TryRemove(satellite.Id, out var removedSatellite);
                GameManager.SendCommandToMap(battleStation.Spacemap.Id, AssetRemoveCommand.write(satellite.GetAssetType(), satellite.Id));
            }

            QueryManager.BattleStations.Modules(battleStation);
            battleStation.RefreshBoosterInterface();
            battleStation.SendClanInterfaceCommand(player);
        }
    }
}
