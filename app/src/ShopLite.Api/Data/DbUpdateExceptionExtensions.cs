using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ShopLite.Api.Data;

public static class DbUpdateExceptionExtensions
{
    /// <summary>True when the failure was a PostgreSQL unique constraint violation (SQLSTATE 23505).</summary>
    public static bool IsUniqueViolation(this DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
