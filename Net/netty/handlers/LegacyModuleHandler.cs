using Ow.Game;
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
    class LegacyModuleHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var read = new LegacyModuleRequest();
            read.readCommand(bytes);

            var player = gameSession.Player;
            string[] packet = read.message.Split('|');
            switch (packet[0])
            {
                case ServerCommands.SET_STATUS:
                    switch (packet[1])
                    {
                        case ClientCommands.CONFIGURATION:
                            player.ChangeConfiguration(packet[2]);
                            break;
                    }
                    break;
                case ServerCommands.ACHIEVEMENTS:
                    if (packet.Length > 2 && packet[1] == ServerCommands.ACHIEVEMENT_BUY)
                    {
                        int achievementId;
                        if (int.TryParse(packet[2], out achievementId))
                            player.Achievements?.HandleBuyRequest(achievementId);
                    }
                    break;
            }
        }
    }
}
