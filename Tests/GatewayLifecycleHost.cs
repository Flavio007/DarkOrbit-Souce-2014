using System;
using System.Diagnostics;
using System.Reflection;
class GatewayLifecycleHost
{
    static int Main(string[] args)
    {
        var type = Assembly.LoadFrom(args[0]).GetType("Ow.Net.WebSocketGateway", true);
        type.GetMethod("Start").Invoke(null, null);
        string command;
        while ((command = Console.ReadLine()) != null)
        {
            var child = (Process)type.GetField("process", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            if (command == "pid") Console.WriteLine("CHILD:" + (child == null || child.HasExited ? 0 : child.Id));
            else if (command == "kill" && child != null) child.Kill();
            else if (command == "stop") break;
        }
        type.GetMethod("Stop").Invoke(null, null);
        return 0;
    }
}
