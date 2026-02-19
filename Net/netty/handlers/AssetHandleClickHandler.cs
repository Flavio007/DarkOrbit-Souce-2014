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
    class AssetHandleClickHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var read = new AssetHandleClickRequest();
            read.readCommand(bytes);

            var player = gameSession.Player;
            if (player == null || player.Spacemap == null)
                return;

            var activatable = player.Spacemap.GetActivatableMapEntity(read.AssetId);

            if (activatable == null)
                return;

            activatable.Click(gameSession);
        }
    }
}
