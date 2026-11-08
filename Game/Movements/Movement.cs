using Ow.Game.Objects;
using Ow.Game.Objects.Players;
using Ow.Net.netty.commands;
using Ow.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Game.Movements
{
    class Movement
    {
        public static void Move(Character character, Position destination)
        {
            lock (character) MoveLocked(character, destination);
        }

        private static void MoveLocked(Character character, Position destination)
        {
            if (character.Speed <= 0)
            {
                character.Moving = false;
                return;
            }

            var start = ActualPosition(character);
            var requested = destination;
            double limit = 1;
            if (character is Player && character.Spacemap != null)
            {
                for(int pass=0;pass<2;pass++)
                    foreach(var poi in character.Spacemap.POIs.Values.OrderBy(zone=>zone.Id))
                        if(poi.Blocks(character.FactionId)) destination=poi.SlideDestination(start,destination);
                foreach (var poi in character.Spacemap.POIs.Values)
                    if (poi.Blocks(character.FactionId))
                        limit = Math.Min(limit, poi.MovementLimit(start, destination));
            }
            if (limit < 1)
                destination = new Position((int)Math.Round(start.X + ((double)destination.X-start.X)*limit), (int)Math.Round(start.Y + ((double)destination.Y-start.Y)*limit));
            character.MovementTime = GetTime(character, destination);

            character.MovementStartTime = DateTime.Now;
            character.Moving = true;

            character.SendCommandToInRangePlayers(MoveCommand.write(character.Id, destination.X, destination.Y, character.MovementTime));
            var player = character as Player;
            if ((destination.X!=requested.X || destination.Y!=requested.Y) && player != null)
                player.SendCommand(MoveCommand.write(character.Id, destination.X, destination.Y, character.MovementTime));
        }

        public static int GetTime(Character character, Position destination)
        {
            try
            {
                character.OldPosition = ActualPosition(character);

                var destinationPosition = destination;
                character.Destination = destinationPosition;

                character.Direction = new Position(destinationPosition.X - character.OldPosition.X, destinationPosition.Y - character.OldPosition.Y);

                var distance = destinationPosition.DistanceTo(character.OldPosition);

                var time = Math.Round(distance / character.Speed * 1000);

                return (int)time;
            }
            catch (Exception e)
            {
                Out.WriteLine("GetTime void exception: " + e, "Movement.cs");
                Logger.Log("error_log", $"- [Movement.cs] GetTime void exception: {e}");
            }
            return -1;
        }

        public static Position Stop(Character character)
        {
            lock (character)
            {
                var position = ActualPosition(character);
                character.Position = character.OldPosition = character.Destination = position;
                character.Direction = new Position(0, 0);
                character.MovementTime = 0;
                character.MovementStartTime = DateTime.Now;
                character.Moving = false;
                return position;
            }
        }

        public static Position ActualPosition(Character character)
        {
            lock (character) return ActualPositionLocked(character);
        }

        private static Position ActualPositionLocked(Character character)
        {
            Position actualPosition;

            if (character.Moving)
            {
                var timeElapsed = (DateTime.Now - character.MovementStartTime).TotalMilliseconds;

                if (timeElapsed < character.MovementTime)
                {
                    actualPosition = new Position((int)Math.Round(character.OldPosition.X + (character.Direction.X * (timeElapsed / character.MovementTime))),
                            (int)Math.Round(character.OldPosition.Y + (character.Direction.Y * (timeElapsed / character.MovementTime))));
                }
                else
                {
                    character.Moving = false;
                    actualPosition = character.Destination;
                }
            }
            else
            {
                actualPosition = character.Position;
            }

            var traveled = (int)Math.Min(int.MaxValue, actualPosition.DistanceTo(character.Position));
            character.Position = actualPosition;
            if (traveled > 0) (character as Player)?.Quests?.ReportProgress("travel", traveled);

            return actualPosition;
        }
    }
}
