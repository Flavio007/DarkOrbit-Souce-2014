using Ow.Game;
using Ow.Net.netty.commands;
using Ow.Net.netty.requests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Net.netty.handlers
{
    class SelectMenuBarItemHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var read = new SelectMenuBarItemRequest();
            read.readCommand(bytes);

            var player = gameSession.Player;
            PacketDebug.NotifyIncoming(player, "SelectMenuBarItemRequest", SelectMenuBarItemRequest.ID);
            if (String.IsNullOrWhiteSpace(read.itemId))
                return;

            if (read.varH4g != SelectMenuBarItemRequest.const_2391 &&
                read.varH4g != SelectMenuBarItemRequest.const_2225)
                return;

            if (read.varat != SelectMenuBarItemRequest.SELECT &&
                read.varat != SelectMenuBarItemRequest.ACTIVATE)
                return;

            if (read.varat == SelectMenuBarItemRequest.SELECT &&
                read.varH4g == SelectMenuBarItemRequest.const_2391)
            {
                if (Ow.Game.Objects.Players.Managers.SettingsManager.LaserCategory.Contains(read.itemId))
                {
                    player.SettingsManager.SetSelectedLaserItem(read.itemId);
                    return;
                }

                if (Ow.Game.Objects.Players.Managers.SettingsManager.RocketsCategory.Contains(read.itemId))
                {
                    player.SettingsManager.SetSelectedRocketItem(read.itemId);
                    return;
                }

                if (Ow.Game.Objects.Players.Managers.SettingsManager.RocketLauncherCategory.Contains(read.itemId))
                {
                    player.SettingsManager.SetSelectedRocketLauncherItem(read.itemId);
                    return;
                }
            }

            // SELECT is also the wire action for toggle-style items. One-shot
            // items must wait for ACTIVATE instead of firing on a simple select.
            if (read.varat == SelectMenuBarItemRequest.SELECT && !IsSelectableOrToggle(read.itemId))
                return;

            player.SettingsManager.UseSlotBarItem(read.itemId);
        }

        private static bool IsSelectableOrToggle(string itemId)
        {
            return Ow.Game.Objects.Players.Managers.SettingsManager.LaserCategory.Contains(itemId) ||
                   Ow.Game.Objects.Players.Managers.SettingsManager.RocketsCategory.Contains(itemId) ||
                   Ow.Game.Objects.Players.Managers.SettingsManager.RocketLauncherCategory.Contains(itemId) ||
                   Ow.Game.Objects.Players.Managers.SettingsManager.FormationsCategory.Contains(itemId) ||
                   Ow.Game.Objects.Players.Managers.SettingsManager.TechsCategory.Contains(itemId) ||
                   Ow.Game.Objects.Players.Managers.SettingsManager.AbilitiesCategory.Contains(itemId) ||
                   itemId == Ow.Game.Objects.Players.Managers.CpuManager.AIM_01_CPU ||
                   itemId == Ow.Game.Objects.Players.Managers.CpuManager.AIM_02_CPU ||
                   itemId == Ow.Game.Objects.Players.Managers.CpuManager.REPBOT_REP_S ||
                   itemId == Ow.Game.Objects.Players.Managers.CpuManager.REPBOT_REP_1 ||
                   itemId == Ow.Game.Objects.Players.Managers.CpuManager.REPBOT_REP_2 ||
                   itemId == Ow.Game.Objects.Players.Managers.CpuManager.REPBOT_REP_3 ||
                   itemId == Ow.Game.Objects.Players.Managers.CpuManager.REPBOT_REP_4 ||
                   itemId == Ow.Game.Objects.Players.Managers.CpuManager.JP_01_CPU ||
                   itemId == Ow.Game.Objects.Players.Managers.CpuManager.JP_02_CPU ||
                   itemId == Ow.Game.Objects.Players.Managers.CpuManager.AJP_01_CPU;
        }
    }
}
