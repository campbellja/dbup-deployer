using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using DbUp;
using DbUp.Engine;
using DbUp.Engine.Output;
using Newtonsoft.Json;

namespace DBDeployer
{
    public class DbExecutor
    {
        private readonly IUpgradeLog _upgradeLog;
        private readonly string _targetDatabaseName;
        private readonly string _connectionString;
        private readonly string _scriptsPath;
        private readonly string _auxScriptPath;
        private readonly  List<SqlScript> _executedScripts;
        private readonly string _configFile;

        public IEnumerable<SqlScript> ExecutedScripts{get { return _executedScripts; }}
        public DbExecutor(AppSettings appSettings, IUpgradeLog upgradeLog)
        {
            _configFile = appSettings.ConfigFile;
            _upgradeLog = upgradeLog;
            _targetDatabaseName = appSettings.TargetDatabaseName;
            _connectionString = appSettings.ConnectionString;
            _scriptsPath = appSettings.ScriptsPath;
            _auxScriptPath = appSettings.AuxiliaryScriptsPath;
            _executedScripts = new List<SqlScript>();
        }
        
        public void Execute(Action createDatabase)
        {
            
            Print("Creating Database...");
            createDatabase();
            
            Print("DONE");

            UpgradeDatabaseWithScriptsFrom(_scriptsPath);

            //users, roles, user-roles, cert, key, grants
            RunAuxScripts();

        }

        private void UpgradeDatabaseWithScriptsFrom(string path)
        {
            var upgrader = DeployChanges
                .To
                .SqlDatabase(_connectionString)
                .WithScriptsFromFileSystem(path)
                .LogTo(_upgradeLog)
                .WithVariables(ReadSubstitionsFromConfigFile())
                .Build();

            if (!upgrader.IsUpgradeRequired())
            {
                Print("Upgrade is not required!");
                return;
            }

            Print("Upgrading Database...");
            var result = upgrader.PerformUpgrade();
            Print("DONE");
            _executedScripts.AddRange(result.Scripts);

            if (!result.Successful)
            {
                Console.ForegroundColor = ConsoleColor.Red;

                Print(result.Error);
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Print("Success!");
            Console.ResetColor();
        }

        public IDictionary<string, string> ReadSubstitionsFromConfigFile()
        {
            var result = new Dictionary<string, string>
            {
                {"SERVERNAME", _targetDatabaseName}
            };

            var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(_configFile), new JsonSerializerSettings{});
            return result.Concat(config.UsernameReplacements)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        public class Config
        {
            public Dictionary<string, string> UsernameReplacements { get; set; }
        }

        private void RunAuxScripts()
        {
            Print("Looking for auxiliary scripts to run in {0}", _auxScriptPath);
            Debug.Assert(Directory.Exists(_auxScriptPath), "directory non existant: " + _auxScriptPath);
            Debug.Assert(Directory.EnumerateFiles(_auxScriptPath).Any(), "no scripts to apply in:" + _auxScriptPath);
            Print("...Okay we have auxiliary scripts to run.");

            UpgradeDatabaseWithScriptsFrom(_auxScriptPath);
        }


        private void Print(Exception ex)
        {
            Program.Print(ex);
        }

        private void Print(string lookingForScriptsToRunIn)
        {
            Program.Print(lookingForScriptsToRunIn);
        }

        private void Print(string lookingForScriptsToRunIn, string scriptPath)
        {
            Program.Print(lookingForScriptsToRunIn, scriptPath);
        }


    }
}