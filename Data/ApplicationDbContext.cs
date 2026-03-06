using Microsoft.EntityFrameworkCore;
using ITServiceDeskApp.Models;

namespace ITServiceDeskApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // =====================================================
        // DbSets
        // =====================================================

        public DbSet<User> Users => Set<User>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketHistory> TicketHistories => Set<TicketHistory>();

        // =====================================================
        // Configuración del modelo
        // =====================================================

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =====================================================
            // ÍNDICES
            // =====================================================

            modelBuilder.Entity<Ticket>()
                .HasIndex(t => t.TicketNumber)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // =====================================================
            // RELACIÓN 1:N Ticket → TicketHistory
            // =====================================================

            modelBuilder.Entity<TicketHistory>()
                .HasOne(th => th.Ticket)
                .WithMany(t => t.History) // 👈 Debe coincidir EXACTAMENTE con Ticket.cs
                .HasForeignKey(th => th.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            // =====================================================
            // Configuración adicional opcional (recomendado)
            // =====================================================

            modelBuilder.Entity<Ticket>()
                .Property(t => t.TicketNumber)
                .HasMaxLength(20);

            modelBuilder.Entity<User>()
                .Property(u => u.FullName)
                .HasMaxLength(120);
        }
    }
}