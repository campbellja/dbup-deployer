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
using log4net;
using log4net.Config;

namespace DBDeployer
{
    class Program
    {
        private const string ConnectionStringFormat = "Server={0};Database={1};Trusted_connection=true;Pooling=false";
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static bool _isInteractive;

        static void Main(string[] args)
        {
            XmlConfigurator.Configure();
            var context = new Context();
            if (!InitialiseContext(context, args))
            {
                return;
            }
            var upgrader = new Upgrader(context, new LogToUpgradeLogFile(Log));

            WhenDebugging_AssertThatScriptsExistInFolders(context);

            if (!UserHasConfirmedExecution(context))
            {
                Console.WriteLine("Aborting create database and run upgrade scripts process.");
                return;
            }
            try
            {
                Console.WriteLine("Creating database if non-existent...");
                upgrader.CreateDatabaseIfNonExistent();

                if (upgrader.IsUpgradeRequired())
                {
                    Console.WriteLine("Upgrading Database...");
                    var result = upgrader.PerformUpgrade();

                    if (!result.Successful)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine(result.Error);
                        Console.ResetColor();
                        return;
                    }

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Success! - Upgrade completed");
                    Console.ResetColor();

                    PrintScriptExecutionList(result.Scripts, context.TargetDatabaseName);
                }
                else
                {
                    Console.WriteLine("Upgrade is not required!");
                }
                PromptToTerminateProcess();
            }
            catch (Exception e)
            {
                const string message = "Unhandled exception thrown during upgrade process.";
                Log.Error(message, e);
                Console.Error.WriteLine("{0}: {1}", message, e);
            }
        }

        private static void PromptToTerminateProcess()
        {
            if (_isInteractive)
            {
                Console.WriteLine("Press any key to terminate this utility.");
                Console.Read();
            }
        }

        private static bool InitialiseContext(Context context, string[] args)
        {
            var validCommandLineArguments = ReadCommandLineArguments(args, context);
            if (!validCommandLineArguments)
            {
                ShowUsageInformation();
                return false;
            }
            ReadAppSettings(context);
            return true;
        }

        private static void ReadAppSettings(Context context)
        {
            context.ConfigFile = Path.Combine(Directory.GetCurrentDirectory(), AppConfig.Get<string>("SubstituteVariableFilePath"));
            context.ScriptsPath = Path.Combine(Directory.GetCurrentDirectory(), AppConfig.Get<string>("ScriptsPath"));
            context.AuxiliaryScriptsPath = Path.Combine(Directory.GetCurrentDirectory(), AppConfig.Get<string>("AuxiliaryScriptsPath"));
            context.ReadSubstitionVariablesFromConfigFile();
        }


        [Conditional("DEBUG")]
        private static void WhenDebugging_AssertThatScriptsExistInFolders(Context context)
        {
            foreach (var path in new[] { context.ScriptsPath, context.AuxiliaryScriptsPath })
            {
                Console.WriteLine("Looking for scripts in {0}", path);
                Debug.Assert(Directory.Exists(path), "directory non existant: " + path);
                Debug.Assert(Directory.EnumerateFiles(path).Any(), "no scripts to apply in:" + path);
                Console.WriteLine("...Okay, scripts exist in that directory.");
            }
        }

        private static bool ReadCommandLineArguments(string[] args, Context context)
        {
            var collection = new CommandArgumentCollection(args);
            _isInteractive = collection.HasArgument("interactive");

            var ca = collection.GetFirstArgument("cs");
            if (ca == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("You must provide an argument /cs for the connection string");
                Console.ResetColor();
                return false;
            }

            SqlConnectionStringBuilder connectionStringBuilder;
            try
            {
                var connectionStringValue = ca.Value.Trim();
                connectionStringBuilder = new SqlConnectionStringBuilder(connectionStringValue);
            }
            catch (ArgumentException argEx)
            {
                Log.Error(argEx);
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(
@"Invalid connection string argument for /cs
Argument must have the following format: Server=_Instance Name_;Database=_Catalog Name_;[Trusted_connection=True;Pooling=False]");
                Console.ResetColor();
                return false;
            }

            var targetDatabaseName = connectionStringBuilder.InitialCatalog;
            var dataSource = connectionStringBuilder.DataSource;
            var connectionString = String.Format(ConnectionStringFormat,
                dataSource, targetDatabaseName);

            var crmArg = collection.GetFirstArgument("crmname");
            if (crmArg == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("You must provide an argument /crmname for the crm database name");
                Console.ResetColor();
                return false;
            }

            context.CrmDatabaseName = crmArg.Value.Trim();
            context.TargetDatabaseName = targetDatabaseName;
            context.ConnectionString = connectionString;
            context.DataSource = dataSource;
            return true;
        }


        static bool UserHasConfirmedExecution(Context settings)
        {
            if (!_isInteractive)
            {
                return true;
            }
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Connection string to Target database: {0}", settings.ConnectionString);
            Console.WriteLine("Target Database name: {0}", settings.TargetDatabaseName);
            Console.WriteLine("CRM Database name is: {0}", settings.CrmDatabaseName);
            PrintLine();
            Console.WriteLine("The following variable substitutions (read from {0}) will be applied:", Path.GetFileName(settings.ConfigFile));
            Console.WriteLine(AsString(settings.Variables));
            PrintLine();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Proceed with creating database and running scripts? (Y/N)");
            Console.ResetColor();
            return (Console.ReadLine() ?? "").ToUpperInvariant().Trim().StartsWith("Y");
        }

        private static void PrintLine()
        {
            Console.WriteLine();
        }

        private static string AsString(IDictionary<string, string> dict)
        {
            var result = new StringBuilder();
            dict.ToList().ForEach(d => result.AppendLine(String.Format(CultureInfo.CurrentUICulture, "{0} => {1}", d.Key, d.Value)));
            return result.ToString();
        }

        private static void ShowUsageInformation()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(
@"DBDeployer - Creates a database and executes SQL scripts using DbUp
Remarks: 
    
    [DB Connections]
    This utility will establish two db connections in succession for creating and upgrading the target database.
    The first connection will target the master catalog and attempt to create a new database. 
    The second connection will target the new database and run the upgrade scripts.
    
    [Variable Substitution]
    This utility reads in key-value pairs from a JSON file (default is config.json) in order to perform variable substitution on the SQL upgrade scripts. 
    Any variables bordered with $ (e.g. $Name$) will be replaced with values from the config file (e.g. Name:Value).

Usage:
    db.deployr.exe /cs:{SQL Connection String} /crmname:{CRM Database Name} [/interactive]                            

    /cs:{Target Connection String} : The complete connection string of the target database that 
                                     you are trying to deploy to.

    /crmname:{CRM Database Name}   : The name of CRM database that the target database is using.

    /interactive                   : Prompts user to confirm each step of the upgrade process.

Example: 
    DBDeployer /cs:Server=test-db01\sql12crm01;Database=MyTargetDatabase;Trusted_connection=True;Pooling=False /crmname:[MyTest_MSCRM] /interactive

");
            Console.ResetColor();
        }

        private static string ToStringOneLinePerElement(IEnumerable<SqlScript> scripts)
        {
            var names = new StringBuilder();
            scripts.ToList().ForEach(s => names.AppendLine(String.Format("{0}", s.Name)));
            return names.ToString();
        }

        private static void PrintScriptExecutionList(IEnumerable<SqlScript> scripts, string serverName)
        {
            if (!scripts.Any())
            {
                Console.WriteLine("No scripts were executed successfully on {0}.", serverName);
                return;
            }

            Console.WriteLine(
@"!!! Attention DEVELOPERS !!!!!!!!!!!!!!!!!!!!
The following scripts were successfully executed on {0} - were you expecting any additional scripts to be applied?? Please double-check this list! ", serverName);
            Console.WriteLine(ToStringOneLinePerElement(scripts));
        }
    }
}
