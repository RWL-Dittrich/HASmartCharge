﻿using HASmartCharge.Backend.DB.Models;
using Microsoft.EntityFrameworkCore;

namespace HASmartCharge.Backend.DB;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }
    
    public DbSet<HomeAssistantConnection> HomeAssistantConnections { get; set; }
    public DbSet<Charger> Chargers { get; set; }
    public DbSet<Connector> Connectors { get; set; }
    public DbSet<ChargingTransaction> ChargingTransactions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Charger>(entity =>
        {
            entity.HasMany(c => c.Connectors)
                .WithOne(c => c.Charger)
                .HasForeignKey(c => c.ChargePointId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(c => c.Transactions)
                .WithOne(t => t.Charger)
                .HasForeignKey(t => t.ChargePointId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}