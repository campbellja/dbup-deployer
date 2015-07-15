using System;
using System.ComponentModel;
using System.Configuration;

namespace DBDeployer
{
    public static class AppConfig
    {
        public static T Get<T>(string key)
        {
            var value = ConfigurationManager.AppSettings[key];
            if (String.IsNullOrWhiteSpace(value))
            {
                throw new AppSettingIsMissingException(String.Format("Setting with key {0} is missng", key));
            }
            return (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(value);
        }

        public class AppSettingIsMissingException : Exception
        {
            public AppSettingIsMissingException() : base() { }
            public AppSettingIsMissingException(string message) : base(message) { }
        }
    }
}