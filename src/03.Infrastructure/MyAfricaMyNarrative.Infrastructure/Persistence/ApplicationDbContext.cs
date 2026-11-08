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
            entity.Property(x => x.StorageKey).HasMaxLength(80).IsRequired();
            entity.Property(x => x.OriginalName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Attribution).HasMaxLength(300);
            entity.Property(x => x.PermissionDeclaration).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.StorageKey).IsUnique();
        });

        builder.Entity<Job>(entity =>
        {
            entity.Property(x => x.Kind).HasMaxLength(80).IsRequired();
            entity.Property(x => x.TargetHash).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RowVersion).IsRowVersion();
            entity.HasIndex(x => new { x.Kind, x.ProjectId, x.TargetHash }).IsUnique();
        });
    }

    public async Task<IReadOnlyList<Project>> ListAsync(Guid ownerId, CancellationToken cancellationToken) =>
        await Projects.AsNoTracking().Where(x => x.OwnerUserId == ownerId).OrderByDescending(x => x.UpdatedAtUtc).ToListAsync(cancellationToken);

    public Task<Project?> FindOwnedAsync(Guid projectId, Guid ownerId, CancellationToken cancellationToken) =>
        Projects.Include(x => x.MediaAssets).SingleOrDefaultAsync(x => x.Id == projectId && x.OwnerUserId == ownerId, cancellationToken);

    public Task<MediaAsset?> FindOwnedAssetAsync(Guid assetId, Guid ownerId, CancellationToken cancellationToken) =>
        MediaAssets.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assetId && x.Project.OwnerUserId == ownerId, cancellationToken);

    public Task AddAsync(Project project, CancellationToken cancellationToken) => Projects.AddAsync(project, cancellationToken).AsTask();
    public Task RemoveAsync(Project project, CancellationToken cancellationToken) { Projects.Remove(project); return Task.CompletedTask; }
}
