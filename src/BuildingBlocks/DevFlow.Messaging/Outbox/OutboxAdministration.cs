using Microsoft.EntityFrameworkCore;

namespace DevFlow.Messaging.Outbox;
public sealed class OutboxDispatchControl
{
    public int Id { get; set; } = 1;
    public bool Paused { get; set; }
}

public sealed class OutboxOperation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid? MessageId { get; set; }
    public string Operator { get; set; } = "";
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
}

public static class OutboxAdministration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<OutboxDispatchControl>(b =>
        {
            b.ToTable("OutboxDispatchControls", "reliability");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.HasData(new OutboxDispatchControl());
        });
        model.Entity<OutboxOperation>(b =>
        {
            b.ToTable("OutboxOperations", "reliability");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.Operator).HasMaxLength(160);
            b.Property(x => x.Action).HasMaxLength(64);
            b.Property(x => x.Reason).HasMaxLength(300);
        });
    }

    public static async Task ExecuteAsync(DbContext db, string action, string actor, string reason, CancellationToken ct)
    {
        if (action is not ("pause-publication" or "resume-publication") || actor.Length is < 1 or > 160 || reason.Length is < 1 or > 300)
        {
            throw new ArgumentException("Bounded operator/reason and valid operation required.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var control = await db.Set<OutboxDispatchControl>().FromSqlRaw("""
            SELECT * FROM reliability."OutboxDispatchControls" WHERE "Id" = 1 FOR UPDATE
            """).SingleAsync(ct);
        control.Paused = action == "pause-publication";
        db.Set<OutboxOperation>().Add(new() { Action = action, Operator = actor, Reason = reason, OccurredAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
