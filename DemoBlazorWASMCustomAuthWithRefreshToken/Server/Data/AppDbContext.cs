using DemoBlazorWASMCustomAuthWithRefreshToken.Server.AuthenticationModel;
using DemoBlazorWASMCustomAuthWithRefreshToken.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoBlazorWASMCustomAuthWithRefreshToken.Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Register> Registration { get; set; } = default!;
        public DbSet<TokenInfo> TokenInfo { get; set; } = default!;
        public DbSet<UserRole> UserRoles { get; set; } = default!;
    }
}
