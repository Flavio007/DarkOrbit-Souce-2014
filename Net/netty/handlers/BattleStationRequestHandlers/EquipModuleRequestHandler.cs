using Ow.Game;
using Ow.Game.Movements;
using Ow.Game.Objects;
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
    class EquipModuleRequestHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var player = gameSession?.Player;
            if (player == null)
                return;

            var request = new EquipModuleRequest();
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

            if (!battleStation.TryResolveClanModule(request.itemId, player.Clan, out var module) || module == null)
            {
                player.SendCommand(BattleStationErrorCommand.write(BattleStationErrorCommand.ITEM_NOT_OWNED));
                return;
            }

            if (!battleStation.IsValidModuleSlot(module.Type, request.slotId))
            {
                player.SendPacket("0|A|STD|Hull deve ser instalado no slot 0, Deflector no slot 1, e os demais modulos nos slots externos.");
                return;
            }

            if (module.InUse)
            {
                player.SendCommand(BattleStationErrorCommand.write(BattleStationErrorCommand.ITEM_ALREADY_EQUIPPED_IN_ANOTHER_ASTEROID));
                return;
            }

            var existingSatellite = battleStation.GetSatelliteBySlotId(request.slotId);
            if (existingSatellite != null)
            {
                if (!request.replace)
                {
                    player.SendCommand(BattleStationErrorCommand.write(BattleStationErrorCommand.REPLACE_RIGHT_MISSING));
                    return;
                }

                existingSatellite.Remove(false, true, false);

                if (battleStation.ShouldDisplayModuleAsSatellite(existingSatellite.SlotId))
                {
                    battleStation.Spacemap.Activatables.TryRemove(existingSatellite.Id, out var removedSatellite);
                    GameManager.SendCommandToMap(battleStation.Spacemap.Id, AssetRemoveCommand.write(existingSatellite.GetAssetType(), existingSatellite.Id));
                }
            }

            var satellite = new Satellite(
                battleStation,
                0,
                Satellite.GetName(module.Type),
                battleStation.GetDefaultModuleDesignId(module.Type),
                module.ItemId,
                request.slotId,
                module.Type,
                Satellite.GetPosition(battleStation.Position, request.slotId));

            satellite.OwnerId = player.Id;
            satellite.InstallationSecondsLeft = battleStation.GetClanModuleInstallationSeconds();
            satellite.installationTime = DateTime.Now;
            satellite.AddVisualModifier(VisualModifierCommand.MODULE_INSTALL_EFFECT, 0, "", 0, true);
            satellite.UpgradeLevel = module.UpgradeLevel;

            if (!battleStation.EquippedStationModule.ContainsKey(player.Clan.Id))
                battleStation.EquippedStationModule[player.Clan.Id] = new List<Satellite>();

            battleStation.EquippedStationModule[player.Clan.Id].Add(satellite);

            if (battleStation.ShouldDisplayModuleAsSatellite(satellite.SlotId))
                battleStation.Spacemap.Activatables.TryAdd(satellite.Id, satellite);

            battleStation.UpdateClanModuleUsage(module.ItemId, true, player.Clan);

            QueryManager.BattleStations.Modules(battleStation);

            if (battleStation.ShouldDisplayModuleAsSatellite(satellite.SlotId))
                GameManager.SendCommandToMap(battleStation.Spacemap.Id, satellite.GetAssetCreateCommand());

            battleStation.RefreshBoosterInterface();
            battleStation.SendClanInterfaceCommand(player);
        }
    }
}
