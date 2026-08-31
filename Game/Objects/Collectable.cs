using Ow.Game.Movements;
using Ow.Game.Objects.Collectables;
using Ow.Game.Objects.Players.Managers;
using Ow.Game.Ticks;
using Ow.Managers;
using Ow.Net.netty;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Objects
{
    abstract class Collectable : Object, Tick
    {
        public int CollectableId { get; set; }
        public string Hash { get; set; }
        public bool Respawnable { get; set; }
        public Character Character { get; set; }
        public Player ToPlayer { get; set; }
        public bool Disposed = false;

        public int Seconds => CollectableId == AssetTypeModule.BOXTYPE_PIRATE_BOOTY ? 5 : -1;

        public Collectable(int collectableId, Position position, Spacemap spacemap, bool respawnable, Player toPlayer) : base(Randoms.CreateRandomID(), position, spacemap)
        {
            Hash = Randoms.GenerateHash(10);
            CollectableId = collectableId;
            Respawnable = respawnable;
            ToPlayer = toPlayer;

            if (this is CargoBox)
            {
                Program.TickManager.AddTick(this);
                disposeTime = DateTime.Now;
            }
        }

        public DateTime collectTime = new DateTime();
        public DateTime disposeTime = new DateTime();
        public void Tick()
        {
            if (!Disposed)
            {
                if (this is CargoBox && disposeTime.AddMinutes(2) < DateTime.Now)
                    Dispose();

                if (Character != null && Character.Collecting)
                {
                    if (!Character.Moving)
                    {
                        if (!(this is GreenBooty) || (this is GreenBooty && Character is Player player && player.Equipment.Items.BootyKeys >= 1))
                        {
                            if (collectTime.AddSeconds(Seconds) < DateTime.Now)
                            {
                                var rewardPlayer = Character is Pet pet ? pet.Owner : Character as Player;
                                if (this is Ore && rewardPlayer != null && rewardPlayer.FreeCargo <= 0)
                                {
                                    rewardPlayer.SendCargoFullWarning();
                                    CancelCollection();
                                    return;
                                }

                                Reward(rewardPlayer);
                                if (rewardPlayer != null)
                                {
                                    rewardPlayer.Quests?.ReportProgress("collect");
                                    if (this is BonusBox)
                                        rewardPlayer.Quests?.ReportProgress("collectBonusBox");
                                }
                                Dispose();
                            }
                        }
                        else CancelCollection();
                    }
                    else CancelCollection();
                }
            }

            /*
            if (!Disposed)
                foreach (var character in Spacemap.Characters.Values)
                {
                    if (character.Position.X == Position.X && character.Position.Y == Position.Y && character is Pet)
                        Collect(character as Pet);
                }
                */
        }

        public void CancelCollection()
        {
            Character.Collecting = false;

            var packet = $"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.ASSEMBLE_COLLECTION_BEAM_CANCELLED}|{(Character is Pet ? 1 : 0)}|{Character.Id}";

            if (Character is Player player)
            {
                //player.SendPacket($"0|LM|ST|SLC");
                player.SendPacket(packet);
            }
            else if (Character is Pet petCharacter)
                petCharacter.SendPacketToInRangePlayers(packet);

            if (this is GreenBooty && Character is Player && (Character as Player).Equipment.Items.BootyKeys <= 0)
                (Character as Player).SendPacket("0|A|STM|msg_booty-key-green_auto_buy_not_active");

            Character = null;

            Program.TickManager.RemoveTick(this);
        }

        public void Collect(Character character)
        {
            if (Disposed) return;

            var player = character is Pet pet ? pet.Owner : character as Player;
            if (player != null && player.Storage.Skills.TryGetValue(SkillManager.SPEARHEAD_ULTIMATE_CLOAK, out var ultimateCloakSkill) && ultimateCloakSkill.Active)
                ultimateCloakSkill.Disable();

            Character = character;
            Character.Collecting = true;
            Character.Moving = false;
            collectTime = DateTime.Now;

            var packet = $"0|{ServerCommands.SET_ATTRIBUTE}|{ServerCommands.ASSEMBLE_COLLECTION_BEAM_ACTIVE}|{(Character is Pet ? 1 : 0)}|{Character.Id}|{Seconds}";
            if (Character is Player)
            {
                //player.SendPacket($"0|LM|ST|SLA|{Seconds}");
                player.SendPacket(packet);
            }
            else if (Character is Pet npet)
                npet.SendPacketToInRangePlayers(packet);

            Program.TickManager.AddTick(this);
        }

        public void Dispose()
        {
            Disposed = true;
            Character = null;
            Spacemap.Objects.TryRemove(Id, out var collectable);
            Program.TickManager.RemoveTick(this);
            GameManager.SendCommandToMap(Spacemap.Id, DisposeBoxCommand.write(Hash, true));

            if (Respawnable)
                Respawn();
        }

        public void Respawn()
        {
            Position = GetRespawnPosition();
            Spacemap.Objects.TryAdd(Id, this);

            if (this is CargoBox)
                Program.TickManager.AddTick(this);

            foreach (var gameSession in GameManager.GameSessions.Values.Where(x => x.Player.Storage.InRangeObjects.ContainsKey(Id)))
                gameSession?.Player.Storage.InRangeObjects.TryRemove(Id, out var obj);

            Disposed = false;
        }

        protected virtual Position GetRespawnPosition()
        {
            return Position.Random(Spacemap, 0, 20800, 0, 12800);
        }

        public abstract void Reward(Player player);

        public abstract byte[] GetCollectableCreateCommand();
    }
}
