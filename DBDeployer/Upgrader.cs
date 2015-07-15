using System;
using System.Data.SqlClient;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using DbUp.Helpers;

namespace DBDeployer
{
    class Upgrader
    {
        private readonly Context _context;
        private readonly UpgradeEngine _upgradeEngine;

        public Upgrader(Context context, IUpgradeLog upgradeLog)
        {
            _context = context;
            _upgradeEngine = CreateUpgradeEngine(_context, upgradeLog);
        }

        static UpgradeEngine CreateUpgradeEngine(Context context, IUpgradeLog upgradeLog)
        {
            var connectionString = context.ConnectionString;
            var scriptsPath = context.ScriptsPath;
            var auxScriptPath = context.AuxiliaryScriptsPath;
            var variables = context.Variables;
            return DeployChanges
                .To
                .SqlDatabase(connectionString)
                .WithScriptsFromFileSystem(scriptsPath)
                .WithScriptsFromFileSystem(auxScriptPath)
                .LogTo(upgradeLog)
                .WithVariables(variables)
                .Build();
        }

        private static void ExecuteOnMasterDatabase(Action<AdHocSqlRunner> execute, string connectionString)
        {
            using (var sqlConnection = new SqlConnection(new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ToString()))
            {
                var connection = sqlConnection;
                sqlConnection.Open();
                execute(new AdHocSqlRunner(() => connection.CreateCommand(), "dbo", () => true));
            }
        }

        public void CreateDatabaseIfNonExistent()
        {
            var connectionString = _context.ConnectionString;
            var targetDatabaseName = _context.TargetDatabaseName;

            ExecuteOnMasterDatabase(
                master => master.ExecuteNonQuery(String.Format(
                    @"IF NOT EXISTS(select 1 from sysdatabases where name = '{0}' )
                BEGIN
                    CREATE DATABASE [{0}]
                END
                ", targetDatabaseName)), connectionString);
        }

        public bool IsUpgradeRequired()
        {
            return _upgradeEngine.IsUpgradeRequired();
        }

        public DatabaseUpgradeResult PerformUpgrade()
        {
            return _upgradeEngine.PerformUpgrade();
        }
    }
}