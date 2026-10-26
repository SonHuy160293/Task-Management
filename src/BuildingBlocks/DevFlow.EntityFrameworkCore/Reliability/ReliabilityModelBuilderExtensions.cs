using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevFlow.EntityFrameworkCore.Reliability;

public static class ReliabilityModelBuilderExtensions
{
    public static ModelBuilder ApplyReliabilityConfigurations(this ModelBuilder modelBuilder, ReliabilityDatabaseProvider provider)
    {
        string Q(string name) => provider == ReliabilityDatabaseProvider.SqlServer ? "[" + name + "]" : "\"" + name + "\"";
        string lease = "(" + Q("LeaseToken") + " IS NULL AND " + Q("LeaseUntil") + " IS NULL) OR ("
            + Q("LeaseToken") + " IS NOT NULL AND " + Q("LeaseUntil") + " IS NOT NULL)";

        var outbox = modelBuilder.Entity<OutboxMessage>();
        outbox.HasKey(x => x.MessageId);
        Configure(outbox, "OutboxMessages", provider);
        outbox.ToTable("OutboxMessages", "reliability", table =>
        {
            table.HasCheckConstraint("CK_Outbox_Status", Q("Status") + " IN (0,1,2,3)");
            table.HasCheckConstraint("CK_Outbox_Versions", Q("SchemaVersion") + " > 0 AND " + Q("AggregateRevision") + " > 0");
            table.HasCheckConstraint("CK_Outbox_Attempts", Q("Attempts") + " >= 0");
            table.HasCheckConstraint("CK_Outbox_Lease", "(" + lease + ") AND ((" + Q("Status") + " = 1 AND "
                + Q("LeaseToken") + " IS NOT NULL) OR (" + Q("Status") + " <> 1 AND " + Q("LeaseToken") + " IS NULL))");
            table.HasCheckConstraint("CK_Outbox_PublishedAt", "(" + Q("Status") + " = 2 AND " + Q("PublishedAt")
                + " IS NOT NULL) OR (" + Q("Status") + " <> 2 AND " + Q("PublishedAt") + " IS NULL)");
        });
        outbox.HasIndex(x => new { x.Status, x.NextAttemptAt }).HasDatabaseName("IX_Outbox_Status_NextAttemptAt");
        outbox.HasIndex(x => new { x.Status, x.LeaseUntil }).HasDatabaseName("IX_Outbox_Status_LeaseUntil");

        var inbox = modelBuilder.Entity<InboxMessage>();
        inbox.HasKey(x => new { x.ConsumerName, x.MessageId });
        Configure(inbox, "InboxMessages", provider);
        inbox.ToTable("InboxMessages", "reliability", table =>
        {
            table.HasCheckConstraint("CK_Inbox_Status", Q("Status") + " IN (0,1,2)");
            table.HasCheckConstraint("CK_Inbox_Lease", "(" + lease + ") AND ((" + Q("Status") + " = 0 AND "
                + Q("LeaseToken") + " IS NOT NULL) OR (" + Q("Status") + " <> 0 AND " + Q("LeaseToken") + " IS NULL))");
            table.HasCheckConstraint("CK_Inbox_ProcessedAt", "(" + Q("Status") + " = 1 AND " + Q("ProcessedAt")
                + " IS NOT NULL) OR (" + Q("Status") + " <> 1 AND " + Q("ProcessedAt") + " IS NULL)");
            table.HasCheckConstraint("CK_Inbox_PayloadHash", HashCheck("PayloadHash", provider));
        });
        inbox.HasIndex(x => new { x.Status, x.LeaseUntil }).HasDatabaseName("IX_Inbox_Status_LeaseUntil");

        var idempotency = modelBuilder.Entity<IdempotencyRecord>();
        idempotency.HasKey(x => x.Id);
        Configure(idempotency, "IdempotencyRecords", provider);
        idempotency.ToTable("IdempotencyRecords", "reliability", table =>
        {
            table.HasCheckConstraint("CK_Idempotency_Status", Q("Status") + " IN (0,1)");
            table.HasCheckConstraint("CK_Idempotency_Lease", "(" + lease + ") AND ((" + Q("Status") + " = 0 AND "
                + Q("LeaseToken") + " IS NOT NULL) OR (" + Q("Status") + " = 1 AND " + Q("LeaseToken") + " IS NULL))");
            table.HasCheckConstraint("CK_Idempotency_Result", "(" + Q("Status") + " = 0 AND " + Q("ResultResourceId")
                + " IS NULL AND " + Q("ResultCode") + " IS NULL AND " + Q("CompletedAt") + " IS NULL) OR ("
                + Q("Status") + " = 1 AND " + Q("ResultResourceId") + " IS NOT NULL AND " + Q("ResultCode")
                + " IS NOT NULL AND " + Q("CompletedAt") + " IS NOT NULL)");
            foreach (string hash in new[] { "OwnerScopeHash", "KeyHash", "RequestFingerprint" })
            {
                table.HasCheckConstraint("CK_Idempotency_" + hash, HashCheck(hash, provider));
            }
        });
        idempotency.HasIndex(x => new { x.Operation, x.OwnerScopeHash, x.KeyHash }).IsUnique()
            .HasDatabaseName("UX_Idempotency_Operation_Owner_Key");
        idempotency.HasIndex(x => new { x.Status, x.LeaseUntil }).HasDatabaseName("IX_Idempotency_Status_LeaseUntil");
        return modelBuilder;
    }

    private static void Configure<TEntity>(EntityTypeBuilder<TEntity> builder, string table, ReliabilityDatabaseProvider provider)
        where TEntity : class
    {
        bool sqlServer = provider == ReliabilityDatabaseProvider.SqlServer;
        builder.ToTable(table, "reliability");
        foreach (var property in builder.Metadata.GetProperties().ToArray())
        {
            Type type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            var mapped = builder.Property(property.Name);
            if (type == typeof(Guid))
            {
                mapped.HasColumnType(sqlServer ? "uniqueidentifier" : "uuid").ValueGeneratedNever();
            }
            else if (type == typeof(DateTimeOffset))
            {
                mapped.HasColumnType(sqlServer ? "datetimeoffset(7)" : "timestamp with time zone");
            }
            else if (type.IsEnum)
            {
                mapped.HasConversion(typeof(short)).HasColumnType("smallint");
            }
            else if (type == typeof(string))
            {
                bool hash = property.Name.EndsWith("Hash", StringComparison.Ordinal) || property.Name == "RequestFingerprint";
                int length = hash ? 64 : property.Name switch
                {
                    "ConsumerName" or "Operation" => 100,
                    "MessageType" => 200,
                    _ => 64
                };
                if (property.Name == "PayloadJson")
                {
                    mapped.HasColumnType(sqlServer ? "nvarchar(max)" : "text").IsRequired();
                }
                else
                {
                    mapped.HasMaxLength(length).IsUnicode(false);
                    if (hash)
                    {
                        mapped.IsFixedLength();
                    }

                    if (sqlServer)
                    {
                        mapped.UseCollation("Latin1_General_100_BIN2");
                    }
                }
            }
        }

        builder.Property<Guid>("ConcurrencyToken").IsConcurrencyToken();
    }

    private static string HashCheck(string column, ReliabilityDatabaseProvider provider)
        => provider == ReliabilityDatabaseProvider.SqlServer
            ? "LEN([" + column + "]) = 64 AND [" + column + "] NOT LIKE '%[^0-9a-f]%'"
            : "\"" + column + "\" ~ '^[0-9a-f]{64}$'";
}
