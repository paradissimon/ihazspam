using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Common
{
    public sealed class Configuration
    {
        public int TimeToLiveInMinutes { get; set; }
        public List<string> MailDomains { get; set; }
        public string DatabaseConnectionString { get; set; }
        public string IncomingMailDirectory { get; set; }
        public string MailboxDirectory { get; set; }

        public static Configuration Instance
        {
            get {
                return _lazyInstance.Value;
            }
        }

        private static readonly Lazy<Configuration> _lazyInstance = new Lazy<Configuration>(() => {
            string json;
            var configFile = Environment.GetEnvironmentVariable("IHAZSPAM_JSON_CONFIG_FILE");
            if (configFile == null)
            {
                throw new Exception("An IHAZSPAM_JSON_CONFIG_FILE environment pointing to the configuration file is required.");
            }

            using (var fs = new FileStream(configFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    json = sr.ReadToEnd();
                }
            }

            var config = JsonConvert.DeserializeObject<Configuration>(json);
            Validate(config);
            return config;
        });

        private static void Validate(Configuration c)
        {
            if (c.TimeToLiveInMinutes < 1) throw new ArgumentException("Configuration - TimeToLiveInMinutes must be >= 1.");
            if (c.MailDomains == null || c.MailDomains.Count == 0) throw new ArgumentException("Configuration - MailDomains not set.");
            if (string.IsNullOrWhiteSpace(c.IncomingMailDirectory)) throw new ArgumentException("Configuration - ExtractedMailDirectory not set.");
            if (string.IsNullOrWhiteSpace(c.MailboxDirectory)) throw new ArgumentException("Configuration - ExtractedMailDirectory not set.");
            if (string.IsNullOrWhiteSpace(c.DatabaseConnectionString)) throw new ArgumentException("Configuration - DatabaseConnectionString not set.");

            for (var i = 0; i < c.MailDomains.Count; i++)
            {
                c.MailDomains[i] = c.MailDomains[i].Trim().ToLowerInvariant();
            }

            // Write test incoming mail dir
            foreach (var dir in new[] {
                Tuple.Create("IncomingMailDirectory", c.IncomingMailDirectory),
                Tuple.Create("MailboxDirectory", c.MailboxDirectory) })
            {
                try
                {
                    var filename = Path.Combine(dir.Item2, string.Format("config-validation-{0}.txt", Guid.NewGuid()));
                    using (var fs = new FileStream(filename, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                    {
                        fs.WriteByte(1);
                    }
                    File.Delete(filename);
                }
                catch
                {
                    throw new ArgumentException(string.Format("Configuration - {0}: {1} is not writable.", dir.Item1, dir.Item2));
                }
            }
        }
    }
}
