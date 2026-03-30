using Ow.Game;
using Ow.Game.Objects.Stations;
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
            gameSession?.Player?.SendPacket("0|A|STD|Legacy battle station modules are disabled. Towers are now configured server-side.");
        }
    }
}
