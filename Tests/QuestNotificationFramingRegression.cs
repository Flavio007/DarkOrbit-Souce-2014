using System;
using System.Collections.Generic;
using System.Linq;
using Ow.Net;
using Ow.Net.netty.commands;
class QuestNotificationFramingRegression
{
    static void Main()
    {
        var replacement = new MessageWildcardReplacementModule("%quest_name%", "Primeiros passos", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.PLAIN));
        var command = new MessageLocalizedWildcardCommand("q2_accomplished_quest", new ClientUITooltipTextFormatModule(ClientUITooltipTextFormatModule.LOCALIZED), new List<MessageWildcardReplacementModule> { replacement });
        var message = command.writeCommand();
        if (!message.Skip(2).SequenceEqual(command.write())) throw new Exception("Embedded module format changed");
        if (((message[0] << 8) | message[1]) != message.Length - 2) throw new Exception("Notification missing wire length");
        if (((message[2] << 8) | message[3]) != MessageLocalizedWildcardCommand.ID) throw new Exception("Wrong notification command id");
        var pong = new byte[] { 0, 4, 0x10, 0x80, 0, 0 };
        var stream = message.Concat(pong).ToArray();
        for (int split = 0; split <= stream.Length; split++)
        {
            int count = 0; var buffer = new ClientPacketBuffer();
            buffer.Feed(stream.Take(split).ToArray(), split, frame => count++, () => {});
            buffer.Feed(stream.Skip(split).ToArray(), stream.Length - split, frame => count++, () => {});
            if (count != 2) throw new Exception("Mission completion swallowed following heartbeat");
        }
        Console.WriteLine("Quest completion notification framing passed at every TCP split; following heartbeat remains readable");
    }
}
