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
    class LogoutCancelRequestHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var player = gameSession.Player;
            player.AbortLogout();
        }
    }
}
