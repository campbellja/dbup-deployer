namespace DBDeployer
{
    public class AppSettings
    {
        public string ConnectionString { get; set; }
        public string ScriptsPath { get; set; }
        public string AuxiliaryScriptsPath { get; set; }
        public string TargetDatabaseName { get; set; }
        public string DataSource { get; set; }
        public string ConfigFile { get; set; }
    }
}