using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Npgsql;
namespace DevFlow.Identity.Infrastructure.Persistence.Core;
public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("ApplicationDb")
            ?? throw new InvalidOperationException("Connection string 'ApplicationDb' is missing.");
    }

    public DbConnection CreateConnection()
        => new NpgsqlConnection(_connectionString);
}
