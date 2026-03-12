using ITServiceDeskApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ITServiceDeskApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();
        public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
        public DbSet<Credential> Credentials => Set<Credential>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Ticket>()
                .HasIndex(t => t.TicketNumber)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<InventoryItem>()
                .HasIndex(i => i.AssetCode)
                .IsUnique();

            modelBuilder.Entity<Credential>()
                .HasIndex(c => new { c.Name, c.Type });

            modelBuilder.Entity<TicketHistory>()
                .HasOne(th => th.Ticket)
                .WithMany(t => t.HistoryEntries)
                .HasForeignKey(th => th.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Ticket>()
                .Property(t => t.TicketNumber)
                .HasMaxLength(20);

            modelBuilder.Entity<User>()
                .Property(u => u.FullName)
                .HasMaxLength(120);

            modelBuilder.Entity<InventoryItem>()
                .Property(i => i.AssetCode)
                .HasMaxLength(30);

            modelBuilder.Entity<Credential>()
                .Property(c => c.Name)
                .HasMaxLength(120);

            modelBuilder.Entity<Credential>()
                .Property(c => c.Type)
                .HasMaxLength(80);

            modelBuilder.Entity<TicketHistory>()
                .Property(th => th.FieldChanged)
                .HasMaxLength(100);

            modelBuilder.Entity<TicketHistory>()
                .Property(th => th.OldValue)
                .HasMaxLength(500);

            modelBuilder.Entity<TicketHistory>()
                .Property(th => th.NewValue)
                .HasMaxLength(500);

            modelBuilder.Entity<TicketHistory>()
                .Property(th => th.ChangedBy)
                .HasMaxLength(100);
        }
    }
}
