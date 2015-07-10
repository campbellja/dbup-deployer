using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DbUp.Engine;
using DbUp.Engine.Output;
using DbUp.Helpers;
using log4net;
using log4net.Config;

namespace DBDeployer
{
    class Program
    {
        private static SqlConnection sqlConnection;
        private static SqlConnection masterSqlConnection;
        private static AdHocSqlRunner database;
        private static AdHocSqlRunner master;

        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            var appSettings = new AppSettings
            {
                ConfigFile = Path.Combine(Directory.GetCurrentDirectory(), "config.json"),
                ScriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts"),
                AuxiliaryScriptsPath = Path.Combine(Directory.GetCurrentDirectory(), "AuxScripts")
            };
            if (!AddCommandLineArgumentsToSettings(args, appSettings))
            {
                ShowHelp();
                return;
            }

            AssertThatScriptsExist(appSettings);

            if (!UserWantsToProceedWithExecution(appSettings))
            {
                Print("Aborting database create and scripting process");
                return;
            }
            using (var temporarySqlDatabase = new TemporarySqlDatabase(appSettings.TargetDatabaseName, appSettings.DataSource))
            {
                var db = temporarySqlDatabase;
                var executor = new DbExecutor(appSettings, new LogToUpgradeLogFile(Log));
                executor.Execute(() => db.Create());
                PrintScriptExecutionList(executor.ExecutedScripts, appSettings.TargetDatabaseName);
            }
            Print("Press any key to terminate this utility.");
            Console.Read();
        }

        private static void AssertThatScriptsExist(AppSettings appSettings)
        {
            var scriptsPath = appSettings.ScriptsPath;
            Print("Looking for scripts in {0}", scriptsPath);
            Debug.Assert(Directory.Exists(scriptsPath), "directory non existant: " + scriptsPath);
            Debug.Assert(Directory.EnumerateFiles(scriptsPath).Any(), "no scripts to apply in:" + scriptsPath);
            Print("...Okay, scripts exist in that directory.");
        }


        private static bool AddCommandLineArgumentsToSettings(string[] args, AppSettings appSettings)
        {
            var  collection = new CommandArgumentCollection(args);
            var ca = collection.GetFirstArgument("cs");

            if (ca == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Print("You must provide an argument /cs for the connection string");
                Console.ResetColor();
                return false;
            }

            var connectionStringBuilder = new SqlConnectionStringBuilder(ca.Value.Trim());

            var targetDatabaseName = connectionStringBuilder.InitialCatalog;
            var dataSource = connectionStringBuilder.DataSource;
            var connectionString = String.Format("Server={0};Database={1};Trusted_connection=true;Pooling=false", dataSource, targetDatabaseName);

            
            appSettings.TargetDatabaseName = targetDatabaseName;
            appSettings.ConnectionString = connectionString;
            appSettings.DataSource = dataSource;

            sqlConnection = new SqlConnection(connectionString);
            database = new AdHocSqlRunner(sqlConnection.CreateCommand, "dbo", () => true);

            var builder = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };

            masterSqlConnection = new SqlConnection(builder.ToString());
            master = new AdHocSqlRunner(() => masterSqlConnection.CreateCommand(), "dbo", () => true);

            return true;
        }


        private static bool UserWantsToProceedWithExecution(AppSettings settings)
        {
            Print("Connection string to Target database: {0}", settings.ConnectionString);
            Print("Target Database name: {0}", settings.TargetDatabaseName);
            Print("Proceed with creating database and running scripts? (in {0}) (Y/N)", settings.ScriptsPath);
            return (Console.ReadLine() ?? "").ToUpperInvariant().Trim().StartsWith("Y");
        }
        
        private static void ShowHelp()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Print(
@"db.deployr.exe - SQL database script executor using DbUp
Usage:
    db.deployr.exe /cs:{SQL Connection String}

    /cs:{Target Connection String} : The complete connection string of
                                     the target database you are 
                                     trying to deploy to.   

Example: 
    db.deployr.exe /cs:Server=testdatabase01\mydatabaseinstance;Database=MyTargetDatabase;Trusted_connection=True;Pooling=False

");
            Console.ResetColor();
        }
        
        class LogToUpgradeLogFile : IUpgradeLog
        {

            public LogToUpgradeLogFile(ILog log)
            {
                _log = log;
            }

            private readonly ILog _log;
            public void WriteInformation(string format, params object[] args)
            {
                var message = String.Format(CultureInfo.CurrentUICulture, format, args);
                _log.Info(message);
                Print(message);
            }

            public void WriteError(string format, params object[] args)
            {
                var message = String.Format(CultureInfo.CurrentUICulture, format, args);
                _log.Error(message);
                Print(message);
            }

            public void WriteWarning(string format, params object[] args)
            {
                var message = String.Format(CultureInfo.CurrentUICulture, format, args);
                _log.Warn(message);
                Print(message);
            }
        }

        private static string GetExecutedScriptsAsString(IEnumerable<SqlScript> scripts)
        {
            var names = new StringBuilder();
            scripts.ToList().ForEach(s => names.AppendLine(String.Format("{0}", s.Name)));
            return names.ToString();

        }
        private static void PrintScriptExecutionList(IEnumerable<SqlScript> scripts, string serverName)
        {
            if (!scripts.Any())
            {
                Print("No scripts were executed successfully on {0}.", serverName);
                return;
            }

            Print(
@"!!! Attention DEVELOPERS !!!!!!!!!!!!!!!!!!!!
The following scripts were executed successfully on {0} - were you expecting any additional scripts to be applied?? Please double-check! ", serverName);
            Print(GetExecutedScriptsAsString(scripts));
        }


        public static void Print(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public static void Print(string format)
        {
            Console.WriteLine(format);
        }


        public static void Print(Exception ex)
        {
            Console.WriteLine(ex);
        }


        //        private static void CreateDatabase()
        //        {
        //            masterSqlConnection.Open();
        //            master.ExecuteNonQuery(String.Format(@"IF NOT EXISTS(select 1 from sysdatabases where name = '{0}' )
        //BEGIN
        //CREATE DATABASE [{0}]
        //END
        //", _targetDatabaseName));

        //            masterSqlConnection.Close();
        //        }
    }
}
