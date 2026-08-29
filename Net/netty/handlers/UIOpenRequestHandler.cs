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
    class UIOpenRequestHandler : IHandler
    {
        public void execute(GameSession gameSession, byte[] bytes)
        {
            var read = new UIOpenRequest();
            read.readCommand(bytes);

            var player = gameSession.Player;
            PacketDebug.NotifyIncoming(player, "UIOpenRequest", UIOpenRequest.ID);
            switch (read.itemId)
            {
                case UIOpenRequest.ACTION_LOGOUT:
                    player.Logout(true);
                    break;
                case UIOpenRequest.ACTION_SHIP_WARP:
                    // This request carries no ship id. Keep ship changes in the
                    // dedicated ship-selection flow instead of changing state here.
                    break;
                case UIOpenRequest.ACTION_REFINEMENT:
                    player.SendOreShopInfo();
                    player.SendModernOreRefinementState();
                    break;
                case UIOpenRequest.ACTION_QUESTS:
                    player.Quests?.OpenWindow();
                    break;
                case UIOpenRequest.ACTION_USER:
                case UIOpenRequest.ACTION_SHIP:
                case UIOpenRequest.ACTION_CHAT:
                case UIOpenRequest.ACTION_GROUP:
                case UIOpenRequest.ACTION_MINIMAP:
                case UIOpenRequest.ACTION_SPACEMAP:
                case UIOpenRequest.ACTION_LOG:
                case UIOpenRequest.ACTION_PET:
                case UIOpenRequest.ACTION_CONTACTS:
                    // These windows are client-owned or handled by another
                    // request flow and have no server-side mutation here.
                    break;
            }
        }
    }
}
