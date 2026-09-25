using FitAi.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FitAi.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider timeProvider) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();
    public DbSet<WorkoutDay> WorkoutDays => Set<WorkoutDay>();
    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();
    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();
    public DbSet<EmailInvite> EmailInvites => Set<EmailInvite>();
    public DbSet<InviteCode> InviteCodes => Set<InviteCode>();
    public DbSet<AuthCode> AuthCodes => Set<AuthCode>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<PaymentRecord> PaymentRecords => Set<PaymentRecord>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
    public DbSet<UsageCounter> UsageCounters => Set<UsageCounter>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums são salvos como texto (ex.: "MONDAY"), como no schema original.
        builder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(200);
            e.Property(u => u.Name).HasMaxLength(200);
            e.HasOne(u => u.Teacher).WithMany(t => t.Students).HasForeignKey(u => u.TeacherId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<ExternalLogin>(e =>
        {
            e.HasIndex(l => new { l.Provider, l.ProviderKey }).IsUnique();
            e.HasOne(l => l.User).WithMany(u => u.ExternalLogins).HasForeignKey(l => l.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WorkoutPlan>(e =>
        {
            e.HasIndex(p => new { p.UserId, p.IsActive });
            e.HasOne(p => p.User).WithMany(u => u.WorkoutPlans).HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.CreatedBy).WithMany().HasForeignKey(p => p.CreatedById)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<WorkoutDay>(e =>
        {
            e.HasOne(d => d.WorkoutPlan).WithMany(p => p.WorkoutDays).HasForeignKey(d => d.WorkoutPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WorkoutExercise>(e =>
        {
            e.HasOne(x => x.WorkoutDay).WithMany(d => d.Exercises).HasForeignKey(x => x.WorkoutDayId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WorkoutSession>(e =>
        {
            e.HasIndex(s => new { s.WorkoutDayId, s.StartedAt });
            e.HasOne(s => s.WorkoutDay).WithMany(d => d.Sessions).HasForeignKey(s => s.WorkoutDayId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EmailInvite>(e =>
        {
            e.HasIndex(i => i.Email);
            e.Property(i => i.Email).HasMaxLength(200);
            e.HasOne(i => i.Teacher).WithMany().HasForeignKey(i => i.TeacherId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(i => i.InvitedBy).WithMany().HasForeignKey(i => i.InvitedById).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<InviteCode>(e =>
        {
            e.HasIndex(c => c.Code).IsUnique();
            e.Property(c => c.Code).HasMaxLength(32);
            e.HasOne(c => c.Teacher).WithMany().HasForeignKey(c => c.TeacherId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<AuthCode>(e =>
        {
            e.HasIndex(c => c.CodeHash).IsUnique();
            e.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Subscription>(e =>
        {
            e.HasIndex(s => s.TeacherId).IsUnique();
            e.HasIndex(s => s.ProviderSubscriptionId).IsUnique();
            e.Property(s => s.Price).HasPrecision(10, 2);
            e.HasOne(s => s.Teacher).WithMany().HasForeignKey(s => s.TeacherId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<PaymentRecord>(e =>
        {
            e.HasIndex(p => p.ProviderPaymentId).IsUnique();
            e.Property(p => p.Value).HasPrecision(10, 2);
            e.HasOne(p => p.Subscription).WithMany(s => s.Payments).HasForeignKey(p => p.SubscriptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WebhookEvent>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Id).HasMaxLength(100);
        });

        b.Entity<UsageCounter>(e =>
        {
            e.HasKey(u => new { u.UserId, u.Period, u.Kind });
            e.Property(u => u.Period).HasMaxLength(7);
            e.Property(u => u.Kind).HasMaxLength(40);
        });

        b.Entity<AppSetting>(e =>
        {
            e.HasKey(s => s.Key);
            e.Property(s => s.Key).HasMaxLength(100);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<IHasTimestamps>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
        foreach (var entry in ChangeTracker.Entries<AppSetting>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified) entry.Entity.UpdatedAt = now;
        }
        foreach (var entry in ChangeTracker.Entries<AuthCode>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default) entry.Entity.CreatedAt = now;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
