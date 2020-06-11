using Microsoft.EntityFrameworkCore;

namespace JsonApiDotNetCore.Data
{
    public sealed class DbContextResolver<TDbContext> : IDbContextResolver
        where TDbContext : DbContext
    {
        private readonly TDbContext _context;

        public DbContextResolver(TDbContext context)
        {
            _context = context;
        }

        public DbContext GetContext() => _context;
    }
}
