﻿using DebtBot.DB.Entities;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.DB
{
    public class DebtContext : DbContext
    {
        public DbSet<Bill> Bills { get; set; }
        public DbSet<BillPayment> BillPayments { get; set; }
        public DbSet<BillLine> BillLines { get; set; }
        public DbSet<BillLineParticipant> BillLineParticipants { get; set; }
        public DbSet<Currency> Currencies { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<Debt> Debts { get; set; }
        public DbSet<Ledger> Ledgers { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserSubordinate> UserSubordinates { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<BillPayment>().HasKey(bp => new { bp.BillId, bp.UserId });
            modelBuilder.Entity<Ledger>().HasKey(l => new { l.CreditorUserId, l.DebtorUserId, l.Currency });
            modelBuilder.Entity<UserSubordinate>().HasKey(us => new { us.UserId, us.SubordinateUserId });
        }
    }
}