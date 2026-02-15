using HASmartCharge.Backend.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.DB;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
    
    public DbSet<HomeAssistantConnection> HomeAssistantConnections { get; set; }
}