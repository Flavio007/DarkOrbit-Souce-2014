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
            MigrateRecognition();
        }

        // Preserve existing balances once while keeping jackpot independent afterwards.
        public static void MigrateRecognition()
        {
            using (var connection = new MySqlConnection(GenerateConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "CREATE TABLE IF NOT EXISTS server_data_migrations (name VARCHAR(100) NOT NULL PRIMARY KEY) ENGINE=InnoDB";
                    command.ExecuteNonQuery();
                }
                using (var transaction = connection.BeginTransaction())
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "SELECT COUNT(*) FROM server_data_migrations WHERE name='recognition_separate_v1'";
                    if (Convert.ToInt64(command.ExecuteScalar()) != 0) { transaction.Commit(); return; }
                    command.CommandText = "SELECT COUNT(*) FROM player_accounts WHERE data IS NOT NULL AND JSON_VALID(data)=0";
                    if (Convert.ToInt64(command.ExecuteScalar()) != 0)
                        throw new InvalidOperationException("Recognition migration: invalid player data JSON.");
                    command.CommandText = "UPDATE player_accounts SET data=JSON_SET(COALESCE(data, '{}'), '$.recognition', CAST(COALESCE(JSON_UNQUOTE(JSON_EXTRACT(data, '$.jackpot')), '0') AS SIGNED)) WHERE data IS NULL OR JSON_CONTAINS_PATH(data, 'one', '$.recognition')=0";
                    command.ExecuteNonQuery();
                    command.CommandText = "INSERT INTO server_data_migrations(name) VALUES('recognition_separate_v1')";
                    command.ExecuteNonQuery();
                    transaction.Commit();
                }
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
