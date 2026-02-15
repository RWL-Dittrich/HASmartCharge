using HASmartChargeBackend.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace HASmartChargeBackend.DB;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
    
    public DbSet<HomeAssistantConnection> HomeAssistantConnections { get; set; }
}