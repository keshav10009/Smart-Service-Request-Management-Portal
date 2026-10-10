using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ServicePortal.Domain.Entities;

namespace ServicePortal.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<SupportTeam> SupportTeams => Set<SupportTeam>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<RequestComment> RequestComments => Set<RequestComment>();
    public DbSet<RequestAttachment> RequestAttachments => Set<RequestAttachment>();
    public DbSet<RequestStatusHistory> RequestStatusHistories => Set<RequestStatusHistory>();
    public DbSet<AssignmentHistory> AssignmentHistories => Set<AssignmentHistory>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ReferenceSequence> ReferenceSequences => Set<ReferenceSequence>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ApplicationUser configuration
        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
            b.Property(u => u.Department).HasMaxLength(100);
            b.HasOne(u => u.SupportTeam)
                .WithMany(t => t.Members)
                .HasForeignKey(u => u.SupportTeamId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // SupportTeam configuration
        builder.Entity<SupportTeam>(b =>
        {
            b.Property(t => t.Name).HasMaxLength(100).IsRequired();
            b.Property(t => t.Description).HasMaxLength(500);
        });

        // Category configuration
        builder.Entity<Category>(b =>
        {
            b.Property(c => c.Name).HasMaxLength(100).IsRequired();
            b.Property(c => c.Description).HasMaxLength(500);
            b.HasOne(c => c.SupportTeam)
                .WithMany(t => t.Categories)
                .HasForeignKey(c => c.SupportTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ServiceRequest configuration
        builder.Entity<ServiceRequest>(b =>
        {
            b.HasKey(r => r.Id);
            b.HasIndex(r => r.ReferenceNumber).IsUnique();
            b.Property(r => r.ReferenceNumber).HasMaxLength(50).IsRequired();
            b.Property(r => r.Title).HasMaxLength(200).IsRequired();
            b.Property(r => r.Description).IsRequired();
            b.Property(r => r.ResolutionSummary).HasMaxLength(2000);
            b.Property(r => r.ReopenReason).HasMaxLength(1000);
            b.Property(r => r.CorrectionNotes).HasMaxLength(2000);

            // Optimistic concurrency token
            b.Property(r => r.RowVersion)
                .IsConcurrencyToken();

            // Indexes for fast searching, filtering, and SLA checks
            b.HasIndex(r => r.Status);
            b.HasIndex(r => r.Priority);
            b.HasIndex(r => r.CreatedAt);
            b.HasIndex(r => r.ResolutionDueAt);
            b.HasIndex(r => r.IsSlaBreached);
            b.HasIndex(r => r.RequesterId);
            b.HasIndex(r => r.AssigneeId);
            b.HasIndex(r => r.SupportTeamId);

            b.HasOne(r => r.Requester)
                .WithMany(u => u.RequestedServices)
                .HasForeignKey(r => r.RequesterId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(r => r.Assignee)
                .WithMany(u => u.AssignedServices)
                .HasForeignKey(r => r.AssigneeId)
                .OnDelete(DeleteBehavior.SetNull);

            b.HasOne(r => r.Category)
                .WithMany(c => c.Requests)
                .HasForeignKey(r => r.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(r => r.SupportTeam)
                .WithMany(t => t.Requests)
                .HasForeignKey(r => r.SupportTeamId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // RequestComment configuration
        builder.Entity<RequestComment>(b =>
        {
            b.Property(c => c.Body).HasMaxLength(4000).IsRequired();
            b.HasOne(c => c.ServiceRequest)
                .WithMany(r => r.Comments)
                .HasForeignKey(c => c.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(c => c.Author)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // RequestAttachment configuration
        builder.Entity<RequestAttachment>(b =>
        {
            b.Property(a => a.OriginalFileName).HasMaxLength(255).IsRequired();
            b.Property(a => a.StoredFileName).HasMaxLength(255).IsRequired();
            b.Property(a => a.ContentType).HasMaxLength(100).IsRequired();

            b.HasOne(a => a.ServiceRequest)
                .WithMany(r => r.Attachments)
                .HasForeignKey(a => a.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(a => a.Uploader)
                .WithMany()
                .HasForeignKey(a => a.UploaderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // RequestStatusHistory configuration
        builder.Entity<RequestStatusHistory>(b =>
        {
            b.Property(h => h.Reason).HasMaxLength(1000);
            b.HasOne(h => h.ServiceRequest)
                .WithMany(r => r.StatusHistories)
                .HasForeignKey(h => h.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(h => h.ChangedBy)
                .WithMany()
                .HasForeignKey(h => h.ChangedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // AssignmentHistory configuration
        builder.Entity<AssignmentHistory>(b =>
        {
            b.Property(h => h.Reason).HasMaxLength(1000);
            b.HasOne(h => h.ServiceRequest)
                .WithMany(r => r.AssignmentHistories)
                .HasForeignKey(h => h.ServiceRequestId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(h => h.NewAssignee)
                .WithMany()
                .HasForeignKey(h => h.NewAssigneeId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(h => h.PreviousAssignee)
                .WithMany()
                .HasForeignKey(h => h.PreviousAssigneeId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(h => h.AssignedBy)
                .WithMany()
                .HasForeignKey(h => h.AssignedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Notification configuration
        builder.Entity<Notification>(b =>
        {
            b.Property(n => n.Title).HasMaxLength(200).IsRequired();
            b.Property(n => n.Message).HasMaxLength(1000).IsRequired();
            b.HasIndex(n => new { n.RecipientId, n.IsRead });

            b.HasOne(n => n.Recipient)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.RecipientId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(n => n.RelatedRequest)
                .WithMany()
                .HasForeignKey(n => n.RelatedRequestId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // AuditLog configuration
        builder.Entity<AuditLog>(b =>
        {
            b.HasIndex(a => a.Timestamp);
            b.HasIndex(a => a.CorrelationId);
            b.HasIndex(a => new { a.EntityName, a.EntityId });
        });

        // ReferenceSequence configuration
        builder.Entity<ReferenceSequence>(b =>
        {
            b.HasKey(s => s.Id);
            b.HasIndex(s => s.Year).IsUnique();
            b.Property(s => s.RowVersion).IsConcurrencyToken();
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateConcurrencyTokens();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        UpdateConcurrencyTokens();
        return base.SaveChanges();
    }

    private void UpdateConcurrencyTokens()
    {
        foreach (var entry in ChangeTracker.Entries<ServiceRequest>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }

        foreach (var entry in ChangeTracker.Entries<ReferenceSequence>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
            }
        }
    }
}
