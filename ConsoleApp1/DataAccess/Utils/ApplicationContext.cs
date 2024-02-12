namespace ConsoleApp1.DataAccess.Utils;

using Entities;
using Microsoft.EntityFrameworkCore;


public class ApplicationContext : DbContext
{
    public ApplicationContext()
    {
        //Database.EnsureDeleted();
        Database.EnsureCreated();
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Message> Messages { get; set; } = null!;


    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("DataSource=..\\..\\..\\Data\\Data.db");
    }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
        .HasIndex(u => u.Login)
        .IsUnique();
        modelBuilder.Entity<User>()
        .HasIndex(u => u.Email)
        .IsUnique();
    }
}
