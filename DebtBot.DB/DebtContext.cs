using DebtBot.DB.Entities;
using Microsoft.EntityFrameworkCore;

namespace DebtBot.DB;

public class DebtContext : DbContext
{
    public DbSet<Bill> Bills { get; set; }
    public DbSet<BillPayment> BillPayments { get; set; }
    public DbSet<BillLine> BillLines { get; set; }
    public DbSet<BillLineParticipant> BillLineParticipants { get; set; }
    public DbSet<Currency> Currencies { get; set; }
    public DbSet<Debt> Debts { get; set; }
    public DbSet<LedgerRecord> LedgerRecords { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<UserContactLink> UserContactsLinks { get; set; }
    public DbSet<Spending> Spendings { get; set; }
    public DbSet<BillParticipant> BillParticipants { get; set; }

    public DebtContext(DbContextOptions<DebtContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder
            .Entity<Debt>()
            .ToView(Debt.ViewName)
            .HasKey(t => new
            {
                t.CreditorUserId, 
                t.DebtorUserId, 
                t.CurrencyCode
            });
    }
}