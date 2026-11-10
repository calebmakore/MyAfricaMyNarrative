using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Identity;

namespace MyAfricaMyNarrative.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IProjectRepository
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<TranscriptRevision> TranscriptRevisions => Set<TranscriptRevision>();
    public DbSet<StoryRevision> StoryRevisions => Set<StoryRevision>();
    public DbSet<StoryScene> StoryScenes => Set<StoryScene>();
    public DbSet<ReviewApproval> ReviewApprovals => Set<ReviewApproval>();
    public DbSet<LanguageReview> LanguageReviews => Set<LanguageReview>();
    public DbSet<ResourceItem> ResourceItems => Set<ResourceItem>();
    public DbSet<ExportArtifact> ExportArtifacts => Set<ExportArtifact>();
    public DbSet<CreatorIntent> CreatorIntents => Set<CreatorIntent>();
    public DbSet<StudioProposal> StudioProposals => Set<StudioProposal>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Project>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.OwnerUserId, x.UpdatedAtUtc });
            entity.HasMany(x => x.MediaAssets).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<MediaAsset>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.StorageKey).HasMaxLength(80).IsRequired();
            entity.Property(x => x.OriginalName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Attribution).HasMaxLength(300);
            entity.Property(x => x.PermissionDeclaration).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.StorageKey).IsUnique();
        });

        builder.Entity<Job>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Kind).HasMaxLength(80).IsRequired();
            entity.Property(x => x.TargetHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PayloadJson).HasMaxLength(16000).IsRequired();
            entity.Property(x => x.LeaseOwner).HasMaxLength(120);
            entity.Property(x => x.LastFailure).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.Kind, x.ProjectId, x.TargetHash }).IsUnique();
        });

        builder.Entity<TranscriptRevision>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Text).HasMaxLength(12000).IsRequired();
            entity.Property(x => x.Language).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.AssetId, x.CreatedAtUtc });
        });
        builder.Entity<StoryRevision>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.SnapshotHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.RevisionNumber }).IsUnique();
            entity.HasMany(x => x.Scenes).WithOne().HasForeignKey(x => x.StoryRevisionId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<StoryScene>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Text).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Caption).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.StoryRevisionId, x.Order }).IsUnique();
        });
        builder.Entity<ReviewApproval>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.SnapshotHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.StoryRevisionId, x.SnapshotHash }).IsUnique();
        });
        builder.Entity<LanguageReview>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.SnapshotHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(x => x.ReviewerName).HasMaxLength(120).IsRequired(); entity.Property(x => x.Language).HasMaxLength(40).IsRequired();
            entity.Property(x => x.FluencyBasis).HasMaxLength(200).IsRequired(); entity.Property(x => x.Notes).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.StoryRevisionId, x.SnapshotHash }).IsUnique();
        });
        builder.Entity<ResourceItem>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Availability).HasMaxLength(300).IsRequired();
        });
        builder.Entity<ExportArtifact>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.SnapshotHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(x => x.StorageKey).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(x => x.StorageKey).IsUnique();
        });
        builder.Entity<CreatorIntent>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.Audience).HasMaxLength(500);
            entity.Property(x => x.Meaning).HasMaxLength(1000); entity.Property(x => x.Tone).HasMaxLength(200);
            entity.Property(x => x.LocalContext).HasMaxLength(1500); entity.Property(x => x.ProtectedTerms).HasMaxLength(1000);
            entity.HasIndex(x => x.ProjectId).IsUnique();
        });
        builder.Entity<StudioProposal>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.BaselineHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(2000).IsRequired(); entity.Property(x => x.PayloadJson).HasMaxLength(16000).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
        });
    }

    public async Task<IReadOnlyList<Project>> ListAsync(Guid ownerId, CancellationToken cancellationToken) =>
        await Projects.AsNoTracking().Where(x => x.OwnerUserId == ownerId).OrderByDescending(x => x.UpdatedAtUtc).ToListAsync(cancellationToken);

    public Task<Project?> FindOwnedAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken) =>
        Projects.Include(x => x.MediaAssets).SingleOrDefaultAsync(x => x.Id == projectId && x.OwnerUserId == ownerId, cancellationToken);

    public Task<MediaAsset?> FindOwnedAssetAsync(Guid assetId, Guid ownerId, CancellationToken cancellationToken) =>
        MediaAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assetId && x.Project.OwnerUserId == ownerId, cancellationToken);

    public Task<Project?> FindOwnedStudioAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken) =>
        Projects.AsSplitQuery().Include(x => x.MediaAssets).Include(x => x.TranscriptRevisions)
            .Include(x => x.StoryRevisions).ThenInclude(x => x.Scenes).Include(x => x.ReviewApprovals)
            .Include(x => x.LanguageReviews)
            .Include(x => x.Resources).Include(x => x.CreatorIntents).Include(x => x.StudioProposals)
            .SingleOrDefaultAsync(x => x.Id == projectId && x.OwnerUserId == ownerId, cancellationToken);

    public Task<Job?> FindOwnedJobAsync(Guid jobId, Guid ownerId, CancellationToken cancellationToken) =>
        Jobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == jobId && x.OwnerUserId == ownerId, cancellationToken);

    public Task<Job?> FindOwnedJobForUpdateAsync(Guid projectId, Guid ownerId, string kind, string targetHash, CancellationToken cancellationToken) =>
        Jobs.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.OwnerUserId == ownerId && x.Kind == kind && x.TargetHash == targetHash, cancellationToken);

    public Task<Job?> FindOwnedJobForUpdateAsync(Guid jobId, Guid ownerId, CancellationToken cancellationToken) =>
        Jobs.SingleOrDefaultAsync(x => x.Id == jobId && x.OwnerUserId == ownerId, cancellationToken);

    public Task<ExportArtifact?> FindOwnedArtifactAsync(Guid artifactId, Guid ownerId, CancellationToken cancellationToken) =>
        ExportArtifacts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == artifactId && Projects.Any(p => p.Id == x.ProjectId && p.OwnerUserId == ownerId), cancellationToken);

    public async Task<IReadOnlyList<Job>> ListOwnedJobsAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken) =>
        await Jobs.AsNoTracking().Where(x => x.ProjectId == projectId && x.OwnerUserId == ownerId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ExportArtifact>> ListOwnedArtifactsAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken) =>
        await ExportArtifacts.AsNoTracking().Where(x => x.ProjectId == projectId && Projects.Any(p => p.Id == projectId && p.OwnerUserId == ownerId))
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(cancellationToken);

    public Task AddAsync(Project project, CancellationToken cancellationToken) => Projects.AddAsync(project, cancellationToken).AsTask();
    public Task AddJobAsync(Job job, CancellationToken cancellationToken) => Jobs.AddAsync(job, cancellationToken).AsTask();
    public Task RemoveAsync(Project project, CancellationToken cancellationToken) { Projects.Remove(project); return Task.CompletedTask; }
}
