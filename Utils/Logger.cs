using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Utils
{
    class Logger
    {
        private static readonly object WriteLock = new object();
        public static void Log(string fileName, string message)
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar + "logs" + Path.DirectorySeparatorChar;

            fileName += $"_{DateTime.Now.ToString("dd.MM.yyyy")}.txt";

            try
            {
                lock (WriteLock)
                {
                    Directory.CreateDirectory(path);
                    using (StreamWriter sw = File.AppendText(Path.Combine(path, fileName)))
                    {
                        sw.WriteLine($"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] " + message);
                    }
                }
            }
            catch (Exception e)
            {
                Out.WriteLine("Log void exception: " + e, "Logger.cs");
            }
        }
    }
}
