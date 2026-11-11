using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

// Run this same assembly on Windows and on the Mono ARM64 deployment candidate.
class LongRunningRegression
{
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }
    static int Main(string[] args)
    {
        try
        {
            var game = Assembly.LoadFrom(args[0]);
            var randoms = game.GetType("Ow.Utils.Randoms", true);
            var ids = new ConcurrentDictionary<int, byte>();
            var hashes = new ConcurrentDictionary<string, byte>();
            var next = randoms.GetMethod("CreateRandomID");
            var hash = randoms.GetMethod("GenerateHash");
            var random = (Random)randoms.GetField("random").GetValue(null);
            Parallel.For(0, 150000, i =>
            {
                var id = (int)next.Invoke(null, null);
                if (id <= 0 || !ids.TryAdd(id, 0)) throw new Exception("Invalid/duplicate entity id");
                var key = (string)hash.Invoke(null, new object[] { 10 });
                if (key.Length != 10 || !hashes.TryAdd(key, 0)) throw new Exception("Invalid/duplicate collectable hash");
                int value = random.Next(-500, 500);
                if (value < -500 || value >= 500) throw new Exception("Concurrent Random corrupted");
            });
            Check(ids.Count == 150000 && hashes.Count == 150000,
                "150,000 concurrent entities exceed the former pool without recursion or duplicate ids/hashes");
            Check(randoms.GetField("CreatedIDs", BindingFlags.Static | BindingFlags.NonPublic) == null &&
                  randoms.GetField("GeneratedHashes", BindingFlags.Static | BindingFlags.NonPublic) == null,
                "retired entities do not accumulate in historical id/hash lists");
            var type = game.GetType("Ow.Game.Events.ScoremageddonState", true);
            var state = Activator.CreateInstance(type, true);
            type.GetField("Combo").SetValue(state, 1);
            var percent = type.GetMethod("RemainingPercent");
            Check((int)percent.Invoke(state, new object[] { DateTime.Now }) == 0,
                "uninitialized/ancient event time expires before integer conversion");
            type.GetField("LastKill").SetValue(state, DateTime.Now);
            Check((int)percent.Invoke(state, new object[] { DateTime.Now.AddYears(80) }) == 0,
                "long-expired event time remains expired on ARM64");
            Check((int)percent.Invoke(state, new object[] { DateTime.Now.AddMinutes(-1) }) == 100,
                "clock rollback cannot exceed 100 percent");
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
