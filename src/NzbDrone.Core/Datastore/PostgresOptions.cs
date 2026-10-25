using Microsoft.Extensions.Configuration;

namespace NzbDrone.Core.Datastore
{
    public class PostgresOptions
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string MainDb { get; set; }
        public string LogDb { get; set; }
        public string MainDbConnectionString { get; set; }
        public string LogDbConnectionString { get; set; }

        public static PostgresOptions GetOptions()
        {
            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            var postgresOptions = new PostgresOptions();
            var section = config.GetSection("Controllarr:Postgres");
            if (!section.Exists() || string.IsNullOrEmpty(section["Host"]))
            {
                section = config.GetSection("Sonarr:Postgres");
            }

            section.Bind(postgresOptions);

            return postgresOptions;
        }
    }
}
