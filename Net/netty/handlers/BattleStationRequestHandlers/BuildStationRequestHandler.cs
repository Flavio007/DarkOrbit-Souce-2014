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
    class BuildStationRequestHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var player = gameSession?.Player;
            if (player == null)
                return;

            var request = new BuildStationRequest();
            request.readCommand(bytes);

            var battleStation = player.Spacemap?.GetActivatableMapEntity(request.battleStationId) as BattleStation;
            if (battleStation == null || !battleStation.IsClanBattleStation)
            {
                player.SendPacket("0|A|STD|Faction battle stations continue funcionando como hoje.");
                return;
            }

            if (player.Clan == null || player.Clan.Id == 0)
            {
                player.SendCommand(BattleStationErrorCommand.write(BattleStationErrorCommand.NO_CLAN));
                return;
            }

            if (!BattleStation.CanClanUseMap(player.Clan, player.Spacemap))
            {
                player.SendPacket("0|A|STD|Seu cla so pode construir esta estacao em mapas neutros ou aliados.");
                return;
            }

            if (!battleStation.CanBeBuiltBy(player))
            {
                player.SendCommand(BattleStationErrorCommand.write(BattleStationErrorCommand.OUT_OF_RANGE));
                return;
            }

            battleStation.QueueClanBuild(player.Clan, player.FactionId, request.buildTimeInMinutes);
            QueryManager.BattleStations.BattleStation(battleStation);

            if (battleStation.InBuildingState)
            {
                GameManager.SendCommandToMap(player.Spacemap.Id, BattleStationBuildingStateCommand.write(
                    battleStation.Id,
                    battleStation.Id,
                    battleStation.Name,
                    Math.Max(0, request.buildTimeInMinutes * 60),
                    Math.Max(0, request.buildTimeInMinutes * 60),
                    player.Clan.Name,
                    new FactionModule((short)battleStation.FactionId)));
            }
        }
    }
}
