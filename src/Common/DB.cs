using Npgsql;

namespace Common
{
    public sealed class DB
    {
        public static NpgsqlConnection CreateConnection()
        {
            var cs = Configuration.Instance.DatabaseConnectionString;
            return new NpgsqlConnection(cs);
        }

        private DB()
        {
        }
    }
}
