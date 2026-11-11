using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MyAfricaMyNarrative.Application;
using MyAfricaMyNarrative.Domain;
using MyAfricaMyNarrative.Infrastructure.Identity;

namespace MyAfricaMyNarrative.Infrastructure.Persistence;

public static class StudioQueryExtensions
{
    /// <summary>
    /// The complete graph that export readiness depends on. Web and Worker must load the same graph,
    /// otherwise the Worker can disagree with the readiness the creator saw (for example a missing language review).
    /// </summary>
    public static IQueryable<Project> IncludeStudio(this IQueryable<Project> projects) =>
        projects.AsSplitQuery().Include(x => x.MediaAssets).Include(x => x.TranscriptRevisions)
            .Include(x => x.TranscriptionProposals)
            .Include(x => x.StoryRevisions).ThenInclude(x => x.Scenes).Include(x => x.ReviewApprovals)
            .Include(x => x.LanguageReviews)
            .Include(x => x.Resources).Include(x => x.CreatorIntents).Include(x => x.CreatorQuestions).Include(x => x.StudioProposals)
            .Include(x => x.Credits).Include(x => x.CreditsApprovals).Include(x => x.Shots)
            .Include(x => x.ProductionPlans).ThenInclude(x => x.Steps)
            .Include(x => x.Glossary).Include(x => x.PilotFeedback);
}

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options), IProjectRepository
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<MediaAsset> MediaAssets => Set<MediaAsset>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<TranscriptRevision> TranscriptRevisions => Set<TranscriptRevision>();
    public DbSet<TranscriptionProposal> TranscriptionProposals => Set<TranscriptionProposal>();
    public DbSet<StoryRevision> StoryRevisions => Set<StoryRevision>();
    public DbSet<StoryScene> StoryScenes => Set<StoryScene>();
    public DbSet<ReviewApproval> ReviewApprovals => Set<ReviewApproval>();
    public DbSet<LanguageReview> LanguageReviews => Set<LanguageReview>();
    public DbSet<ResourceItem> ResourceItems => Set<ResourceItem>();
    public DbSet<ExportArtifact> ExportArtifacts => Set<ExportArtifact>();
    public DbSet<CreatorIntent> CreatorIntents => Set<CreatorIntent>();
    public DbSet<CreatorQuestion> CreatorQuestions => Set<CreatorQuestion>();
    public DbSet<StudioProposal> StudioProposals => Set<StudioProposal>();
    public DbSet<CreditEntry> CreditEntries => Set<CreditEntry>();
    public DbSet<CreditsApproval> CreditsApprovals => Set<CreditsApproval>();
    public DbSet<ShotPlanItem> ShotPlanItems => Set<ShotPlanItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Project>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Idea).HasMaxLength(2000).IsRequired();
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
            entity.Property(x => x.SuppliedBy).HasMaxLength(120).IsRequired(); entity.Property(x => x.ContributorNote).HasMaxLength(300).IsRequired();
            entity.HasIndex(x => x.StorageKey).IsUnique();
        });

        builder.Entity<Job>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Kind).HasMaxLength(80).IsRequired();
            entity.Property(x => x.TargetHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.PayloadJson).HasMaxLength(16000).IsRequired();
            entity.Property(x => x.LeaseOwner).HasMaxLength(120);
            entity.Property(x => x.ProgressNote).HasMaxLength(Job.MaxProgressNoteLength).IsRequired();
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
        builder.Entity<TranscriptionProposal>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.Text).HasMaxLength(12000).IsRequired();
            entity.Property(x => x.DetectedLanguages).HasMaxLength(200); entity.Property(x => x.Provider).HasMaxLength(80).IsRequired(); entity.Property(x => x.Model).HasMaxLength(120).IsRequired();
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
            entity.Property(x => x.MorePictures).HasMaxLength(200).IsRequired();
            entity.Ignore(x => x.MorePictureIds); entity.Ignore(x => x.Pictures);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Text).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.Caption).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Translation).HasMaxLength(2000).IsRequired();
            entity.Ignore(x => x.EffectiveCaption);
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
            entity.Property(x => x.CreditsHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.StorageKey).IsUnique();
        });
        builder.Entity<CreatorIntent>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.Audience).HasMaxLength(500);
            entity.Property(x => x.Meaning).HasMaxLength(1000); entity.Property(x => x.Tone).HasMaxLength(200);
            entity.Property(x => x.LocalContext).HasMaxLength(1500); entity.Property(x => x.ProtectedTerms).HasMaxLength(1000);
            entity.Property(x => x.TranslationLanguage).HasMaxLength(40).IsRequired();
            entity.HasIndex(x => x.ProjectId).IsUnique();
        });
        builder.Entity<CreatorQuestion>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.Field).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Prompt).HasMaxLength(300).IsRequired(); entity.Property(x => x.Answer).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
        });
        builder.Entity<StudioProposal>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.BaselineHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(2000).IsRequired(); entity.Property(x => x.PayloadJson).HasMaxLength(16000).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
        });
        builder.Entity<CreditEntry>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.Role).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.Order });
        });
        builder.Entity<CreditsApproval>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.CreditsHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.CreditsHash }).IsUnique();
        });
        builder.Entity<ShotPlanItem>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.SceneTitle).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired(); entity.Property(x => x.ResourceIdList).HasMaxLength(800).IsRequired();
            entity.Ignore(x => x.ResourceIds);
            entity.HasIndex(x => new { x.ProjectId, x.CreatedAtUtc });
        });
        builder.Entity<PilotFeedback>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.HelpNote).HasMaxLength(500).IsRequired(); entity.Property(x => x.Comment).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => x.ProjectId).IsUnique();
        });
        builder.Entity<GlossaryEntry>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.Term).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Language).HasMaxLength(40).IsRequired(); entity.Property(x => x.Explanation).HasMaxLength(160).IsRequired();
            entity.Ignore(x => x.IsApproved);
            entity.HasIndex(x => new { x.ProjectId, x.Term }).IsUnique();
        });
        builder.Entity<ProductionPlan>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.StoryHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(x => x.ResourceHash).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(x => x.ResourceSnapshot).HasMaxLength(8000).IsRequired();
            entity.Property(x => x.Summary).HasMaxLength(1500).IsRequired(); entity.Property(x => x.ChangeNote).HasMaxLength(1500).IsRequired();
            entity.Property(x => x.Source).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.Version }).IsUnique();
            entity.HasMany(x => x.Steps).WithOne().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<ProductionPlanStep>(entity =>
        {
            entity.Property(x => x.Id).ValueGeneratedNever(); entity.Property(x => x.SceneTitle).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Instruction).HasMaxLength(600).IsRequired(); entity.Property(x => x.Purpose).HasMaxLength(400).IsRequired();
            entity.Property(x => x.ResourceIdList).HasMaxLength(800).IsRequired();
            entity.Ignore(x => x.ResourceIds);
            entity.HasIndex(x => new { x.PlanId, x.Order }).IsUnique();
        });
    }

    public async Task<IReadOnlyList<Project>> ListAsync(Guid ownerId, CancellationToken cancellationToken) =>
        await Projects.AsNoTracking().Where(x => x.OwnerUserId == ownerId).OrderByDescending(x => x.UpdatedAtUtc).ToListAsync(cancellationToken);

    public Task<Project?> FindOwnedAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken) =>
        Projects.Include(x => x.MediaAssets).SingleOrDefaultAsync(x => x.Id == projectId && x.OwnerUserId == ownerId, cancellationToken);

    public Task<MediaAsset?> FindOwnedAssetAsync(Guid assetId, Guid ownerId, CancellationToken cancellationToken) =>
        MediaAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assetId && x.Project.OwnerUserId == ownerId, cancellationToken);

    public Task<Project?> FindOwnedStudioAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken) =>
        Projects.IncludeStudio().SingleOrDefaultAsync(x => x.Id == projectId && x.OwnerUserId == ownerId, cancellationToken);

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
