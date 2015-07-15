using System;
using System.Globalization;
using DbUp.Engine.Output;
using log4net;

namespace DBDeployer
{
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
        }

        public void WriteError(string format, params object[] args)
        {
            var message = String.Format(CultureInfo.CurrentUICulture, format, args);
            _log.Error(message);
        }

        public void WriteWarning(string format, params object[] args)
        {
            var message = String.Format(CultureInfo.CurrentUICulture, format, args);
            _log.Warn(message);
        }
    }
}