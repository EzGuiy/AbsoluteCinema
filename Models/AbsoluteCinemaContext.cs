using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

namespace Kawin_67091861_002.Models;

public partial class AbsoluteCinemaContext : DbContext
{
    public AbsoluteCinemaContext()
    {
    }

    public AbsoluteCinemaContext(DbContextOptions<AbsoluteCinemaContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Booking> Bookings { get; set; }

    public virtual DbSet<Chat> Chats { get; set; }

    public virtual DbSet<Cinema> Cinemas { get; set; }

    public virtual DbSet<Movie> Movies { get; set; }

    public virtual DbSet<Operation> Operations { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Showtime> Showtimes { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<AccountingTransaction> AccountingTransactions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseMySql(
                "server=localhost;port=3306;database=absolute_cinema;user=root;password=P@22w0rDis(0rrect",
                ServerVersion.Parse("8.0.43-mysql")
            );
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_unicode_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PRIMARY");

            entity.ToTable("bookings");

            entity.HasIndex(e => e.ShowtimeId, "ShowtimeId");

            entity.HasIndex(e => e.UserId, "UserId");

            entity.Property(e => e.BookedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.Status).HasColumnType("enum('Confirmed','Cancelled','RefundPending')");
            entity.Property(e => e.TotalAmount)
                .HasPrecision(10, 2)
                .HasDefaultValueSql("'0.00'");
            entity.Property(e => e.SeatNumbers)
                .HasMaxLength(500);
            entity.Property(e => e.SeatCount)
                .HasDefaultValue(1);

            entity.HasOne(d => d.Showtime).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.ShowtimeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("bookings_ibfk_2");

            entity.HasOne(d => d.User).WithMany(p => p.Bookings)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("bookings_ibfk_1");
        });

        modelBuilder.Entity<Chat>(entity =>
        {
            entity.HasKey(e => e.ChatId).HasName("PRIMARY");

            entity.ToTable("chats");

            entity.HasIndex(e => e.ReceiverId, "ReceiverId");

            entity.HasIndex(e => e.SenderId, "SenderId");

            entity.HasIndex(e => e.TargetRole, "TargetRole");

            entity.Property(e => e.Message).HasColumnType("text");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Receiver).WithMany(p => p.ChatReceivers)
                .HasForeignKey(d => d.ReceiverId)
                .HasConstraintName("chats_ibfk_3");

            entity.HasOne(d => d.Sender).WithMany(p => p.ChatSenders)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("chats_ibfk_1");

            entity.HasOne(d => d.TargetRoleNavigation).WithMany(p => p.Chats)
                .HasForeignKey(d => d.TargetRole)
                .HasConstraintName("chats_ibfk_2");
        });

        modelBuilder.Entity<Cinema>(entity =>
        {
            entity.HasKey(e => e.CinemaId).HasName("PRIMARY");

            entity.ToTable("cinemas");

            entity.Property(e => e.CinemaName).HasMaxLength(50);
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'Ready'")
                .HasColumnType("enum('Ready','Cleaning','Maintenance')");
            entity.Property(e => e.TotalSeats)
                .HasDefaultValueSql("100");
        });

        modelBuilder.Entity<Movie>(entity =>
        {
            entity.HasKey(e => e.MovieId).HasName("PRIMARY");

            entity.ToTable("movies");

            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'Showing'");
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Genre).HasMaxLength(100);
            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.PosterUrl).HasMaxLength(500);
            entity.Property(e => e.TrailerUrl).HasMaxLength(500);
            entity.Property(e => e.PriceMin)
                .HasPrecision(10, 2)
                .HasDefaultValueSql("'0.00'");
            entity.Property(e => e.PriceMax)
                .HasPrecision(10, 2)
                .HasDefaultValueSql("'0.00'");
            entity.Property(e => e.Rating)
                .HasDefaultValueSql("'0'");
            entity.Property(e => e.ReleaseDate).HasColumnType("date");
        });

        modelBuilder.Entity<Operation>(entity =>
        {
            entity.HasKey(e => e.OperationId).HasName("PRIMARY");

            entity.ToTable("operations");

            entity.HasIndex(e => e.CinemaId, "CinemaId");

            entity.HasIndex(e => e.ReportedBy, "ReportedBy");

            entity.Property(e => e.Description).HasColumnType("text");
            entity.Property(e => e.Status)
                .HasDefaultValueSql("'Pending'")
                .HasColumnType("enum('Pending','InProgress','Resolved')");
            entity.Property(e => e.TaskType).HasColumnType("enum('Cleaning','Repair','Inspection')");

            entity.HasOne(d => d.Cinema).WithMany(p => p.Operations)
                .HasForeignKey(d => d.CinemaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("operations_ibfk_1");

            entity.HasOne(d => d.ReportedByNavigation).WithMany(p => p.Operations)
                .HasForeignKey(d => d.ReportedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("operations_ibfk_2");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PRIMARY");

            entity.ToTable("payments");

            entity.HasIndex(e => e.BookingId, "BookingId");

            entity.HasIndex(e => e.TaxInvoiceNo, "TaxInvoiceNo").IsUnique();

            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.IsRefunded).HasDefaultValueSql("'0'");
            entity.Property(e => e.PaymentDate)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.TaxInvoiceNo).HasMaxLength(50);

            entity.HasOne(d => d.Booking).WithMany(p => p.Payments)
                .HasForeignKey(d => d.BookingId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("payments_ibfk_1");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PRIMARY");

            entity.ToTable("roles");

            entity.HasIndex(e => e.RoleName, "RoleName").IsUnique();

            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<Showtime>(entity =>
        {
            entity.HasKey(e => e.ShowtimeId).HasName("PRIMARY");

            entity.ToTable("showtimes");

            entity.HasIndex(e => e.CinemaId, "CinemaId");

            entity.HasIndex(e => e.MovieId, "MovieId");

            entity.Property(e => e.EndTime).HasColumnType("datetime");
            entity.Property(e => e.StartTime).HasColumnType("datetime");

            entity.HasOne(d => d.Cinema).WithMany(p => p.Showtimes)
                .HasForeignKey(d => d.CinemaId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("showtimes_ibfk_2");

            entity.HasOne(d => d.Movie).WithMany(p => p.Showtimes)
                .HasForeignKey(d => d.MovieId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("showtimes_ibfk_1");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PRIMARY");

            entity.ToTable("users");

            entity.HasIndex(e => e.RoleId, "RoleId");

            entity.HasIndex(e => e.Username, "Username").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("datetime");
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Username).HasMaxLength(100);

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("users_ibfk_1");
        });

        modelBuilder.Entity<AccountingTransaction>(entity =>
        {
            entity.HasKey(e => e.TransactionId).HasName("PRIMARY");
            entity.ToTable("accounting_transactions");
            entity.Property(e => e.Amount).HasPrecision(12, 2);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Type).HasMaxLength(20);
            entity.Property(e => e.ReferenceNo).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.TransactionDate).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("datetime");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
