using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ow.Managers.MySQLManager
{
    static class SqlDatabaseManager
    {
        public static string SERVER = Environment.GetEnvironmentVariable("DO_DB_HOST") ?? "127.0.0.1";
        public static string UID = Environment.GetEnvironmentVariable("DO_DB_USER") ?? "root";
        public static string PWD = Environment.GetEnvironmentVariable("DO_DB_PASSWORD") ?? "";
        public static string DB = Environment.GetEnvironmentVariable("DO_DB_NAME") ?? "darkorbit";
        public static bool Initialized = false;

        public static void Initialize()
        {
            // Connector/NET 6.6.5 predates MariaDB's utf8 -> utf8mb3 name.
            // Opt-in for the Linux test host; both names use the same encoding.
            if (Environment.GetEnvironmentVariable("DO_DB_LEGACY_UTF8MB3") == "1")
            {
                var type = typeof(MySqlConnection).Assembly.GetType("MySql.Data.MySqlClient.CharSetMap", true);
                var field = type.GetField("mapping", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                var mapping = (System.Collections.IDictionary)field.GetValue(null);
                if (!mapping.Contains("utf8mb3")) mapping["utf8mb3"] = mapping["utf8"];
            }
            GenerateConnectionString();
            Initialized = true;
            using (var client = GetClient())
            {
                client.ExecuteNonQuery("SELECT 1");
            }
        }

        public static SqlDatabaseClient GetClient()
        {
            var Connection = new MySqlConnection(GenerateConnectionString());
            Connection.Open();
            return new SqlDatabaseClient(Connection);
        }

        public static string GenerateConnectionString()
        {
            if (ConnectionString == "")
            {
                MySqlConnectionStringBuilder ConnectionStringBuilder = new MySqlConnectionStringBuilder();
                ConnectionStringBuilder.Server = SERVER;
                ConnectionStringBuilder.Port = 3306;
                ConnectionStringBuilder.UserID = UID;
                ConnectionStringBuilder.Password = PWD;
                ConnectionStringBuilder.Database = DB;
                var characterSet = Environment.GetEnvironmentVariable("DO_DB_CHARSET");
                if (!string.IsNullOrWhiteSpace(characterSet)) ConnectionStringBuilder.CharacterSet = characterSet;
                ConnectionStringBuilder.ConvertZeroDateTime = true;
                ConnectionStringBuilder.Pooling = true;
                ConnectionStringBuilder.MaximumPoolSize = 100;
                ConnectionStringBuilder.SslMode = MySqlSslMode.None;
                ConnectionString = ConnectionStringBuilder.ToString();
            }
            return ConnectionString;
        }

        public static string ConnectionString = "";

    }
}