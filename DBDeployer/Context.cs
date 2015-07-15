using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace DBDeployer
{
    public class Context
    {
        public string ConnectionString { get; set; }
        public string ScriptsPath { get; set; }
        public string AuxiliaryScriptsPath { get; set; }
        public string DataSource { get; set; }
        public string ConfigFile { get; set; }

        public string TargetDatabaseName { get; set; }
        public string CrmDatabaseName { get; set; }

        public IDictionary<string, string> Variables { get; private set; }
        

        public void ReadSubstitionVariablesFromConfigFile()
        {
            var configFilePath = ConfigFile;
            var result = new Dictionary<string, string>
            {
                {"SERVERNAME", TargetDatabaseName},
                {"CRMDBNAME", CrmDatabaseName}
            };

            var config =
                JsonConvert.DeserializeObject<Config>(File.ReadAllText(configFilePath), new JsonSerializerSettings { });
            Variables = result.Concat(config.UsernameReplacements)
                .ToDictionary(x => x.Key, x => x.Value);
        }

        public class Config
        {
            public Dictionary<string, string> UsernameReplacements { get; set; }
        }
    }
}