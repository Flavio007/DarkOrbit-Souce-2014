using System;
using Ow.Game.Ticks;
namespace Ow.Utils {
    static class Out {public static void WriteLine(string text,string source){}}
    static class Logger {public static int Errors;public static void Log(string category,string text){Errors++;}}
}
class TickManagerRegression {
    class ActionTick:Tick {readonly Action action;public ActionTick(Action value){action=value;}public void Tick(){action();}}
    static int Main(){
        var manager=new TickManager();int runs=0;var bad=new ActionTick(()=>{throw new Exception("test failure");});
        var good=new ActionTick(()=>runs++);manager.AddTick(bad);manager.AddTick(good);manager.AddTick(good);
        manager.TickOnce();manager.TickOnce();
        if(runs!=2||Ow.Utils.Logger.Errors!=1)throw new Exception("Failed object stopped updates or log throttling failed");
        manager.RemoveTick(bad);var next=new ActionTick(()=>runs+=10);
        var adding=new ActionTick(()=>manager.AddTick(next));manager.AddTick(adding);manager.TickOnce();
        if(runs!=3)throw new Exception("New tick ran during registration snapshot");
        manager.RemoveTick(adding);manager.TickOnce();if(runs!=14||!manager.Exists(next))throw new Exception("Tick registration/removal failed");
        Console.WriteLine("Tick fault isolation, repeated errors, registration and removal passed");return 0;
    }
}
