using DevFlow.SharedKernel.Abstractions.Domain;
using DevFlow.SharedKernel.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace DevFlow.EntityFrameworkCore.Interceptors;

public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IServiceProvider _serviceProvider;

    public AuditSaveChangesInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context, GetCurrentUserId());
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context, GetCurrentUserId());
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private string? GetCurrentUserId()
    {
        var user = _serviceProvider.GetService<ICurrentUserProvider>()?.GetCurrentUser();

        return !string.IsNullOrWhiteSpace(user?.Id)
            ? user.Id
            : null;
    }

    private static void ApplyAudit(DbContext? dbContext, string? user)
    {
        if (dbContext is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var entry in dbContext.ChangeTracker.Entries())
        {
            ApplySoftDelete(entry, now, user);
            ApplyAuditFields(entry, now, user);
        }
    }

    private static void ApplySoftDelete(EntityEntry entry, DateTimeOffset now, string? user)
    {
        if (entry.State != EntityState.Deleted || entry.Entity is not ISoftDeletableEntity softDeletableEntity)
        {
            return;
        }

        entry.State = EntityState.Modified;
        softDeletableEntity.SetDeletedAudit(now, user);
    }

    private static void ApplyAuditFields(EntityEntry entry, DateTimeOffset now, string? user)
    {
        if (entry.Entity is not IAuditableEntity auditableEntity)
        {
            return;
        }

        if (entry.State == EntityState.Added)
        {
            var createdAt = auditableEntity.CreatedAt == default ? now : auditableEntity.CreatedAt;
            auditableEntity.SetCreatedAudit(createdAt, auditableEntity.CreatedBy ?? user);
            return;
        }

        if (entry.State == EntityState.Modified)
        {
            auditableEntity.SetUpdatedAudit(now, user);

            if (entry.Entity is ISoftDeletableEntity { IsDeleted: true, DeletedAt: null } softDeletableEntity)
            {
                softDeletableEntity.SetDeletedAudit(now, user);
            }
        }
    }
}
