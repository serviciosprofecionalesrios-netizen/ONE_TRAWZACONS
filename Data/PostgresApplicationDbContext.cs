using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Data
{
    /// <summary>
    /// PostgreSQL-specific context used by Render. Keeping a distinct context
    /// gives PostgreSQL its own migration history without changing SQL Server's.
    /// </summary>
    public sealed class PostgresApplicationDbContext : ApplicationDbContext
    {
        public PostgresApplicationDbContext(DbContextOptions<PostgresApplicationDbContext> options)
            : base(options)
        {
        }
    }
}
