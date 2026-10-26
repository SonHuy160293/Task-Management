using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevFlow.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIdentityEmailOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reliability");

            migrationBuilder.CreateTable(
                name: "EmailDeliveryReceipts",
                schema: "reliability",
                columns: table => new
                {
                    DeliveryMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDeliveryReceipts", x => x.DeliveryMessageId);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                schema: "reliability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    OwnerScopeHash = table.Column<string>(type: "character(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    KeyHash = table.Column<string>(type: "character(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ResultResourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                    table.CheckConstraint("CK_Idempotency_KeyHash", "\"KeyHash\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_Idempotency_Lease", "((\"LeaseToken\" IS NULL AND \"LeaseUntil\" IS NULL) OR (\"LeaseToken\" IS NOT NULL AND \"LeaseUntil\" IS NOT NULL)) AND ((\"Status\" = 0 AND \"LeaseToken\" IS NOT NULL) OR (\"Status\" = 1 AND \"LeaseToken\" IS NULL))");
                    table.CheckConstraint("CK_Idempotency_OwnerScopeHash", "\"OwnerScopeHash\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_Idempotency_RequestFingerprint", "\"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_Idempotency_Result", "(\"Status\" = 0 AND \"ResultResourceId\" IS NULL AND \"ResultCode\" IS NULL AND \"CompletedAt\" IS NULL) OR (\"Status\" = 1 AND \"ResultResourceId\" IS NOT NULL AND \"ResultCode\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_Idempotency_Status", "\"Status\" IN (0,1)");
                });

            migrationBuilder.CreateTable(
                name: "InboxMessages",
                schema: "reliability",
                columns: table => new
                {
                    ConsumerName = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadHash = table.Column<string>(type: "character(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxMessages", x => new { x.ConsumerName, x.MessageId });
                    table.CheckConstraint("CK_Inbox_Lease", "((\"LeaseToken\" IS NULL AND \"LeaseUntil\" IS NULL) OR (\"LeaseToken\" IS NOT NULL AND \"LeaseUntil\" IS NOT NULL)) AND ((\"Status\" = 0 AND \"LeaseToken\" IS NOT NULL) OR (\"Status\" <> 0 AND \"LeaseToken\" IS NULL))");
                    table.CheckConstraint("CK_Inbox_PayloadHash", "\"PayloadHash\" ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("CK_Inbox_ProcessedAt", "(\"Status\" = 1 AND \"ProcessedAt\" IS NOT NULL) OR (\"Status\" <> 1 AND \"ProcessedAt\" IS NULL)");
                    table.CheckConstraint("CK_Inbox_Status", "\"Status\" IN (0,1,2)");
                });

            migrationBuilder.CreateTable(
                name: "OutboxDispatchControls",
                schema: "reliability",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Paused = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxDispatchControls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "reliability",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(200)", unicode: false, maxLength: 200, nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    AggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateRevision = table.Column<long>(type: "bigint", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", unicode: false, maxLength: 64, nullable: true),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.MessageId);
                    table.CheckConstraint("CK_Outbox_Attempts", "\"Attempts\" >= 0");
                    table.CheckConstraint("CK_Outbox_Lease", "((\"LeaseToken\" IS NULL AND \"LeaseUntil\" IS NULL) OR (\"LeaseToken\" IS NOT NULL AND \"LeaseUntil\" IS NOT NULL)) AND ((\"Status\" = 1 AND \"LeaseToken\" IS NOT NULL) OR (\"Status\" <> 1 AND \"LeaseToken\" IS NULL))");
                    table.CheckConstraint("CK_Outbox_PublishedAt", "(\"Status\" = 2 AND \"PublishedAt\" IS NOT NULL) OR (\"Status\" <> 2 AND \"PublishedAt\" IS NULL)");
                    table.CheckConstraint("CK_Outbox_Status", "\"Status\" IN (0,1,2,3)");
                    table.CheckConstraint("CK_Outbox_Versions", "\"SchemaVersion\" > 0 AND \"AggregateRevision\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "OutboxOperations",
                schema: "reliability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Operator = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxRecoveries",
                schema: "reliability",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequeuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxRecoveries", x => x.Id);
                });

            migrationBuilder.InsertData(
                schema: "reliability",
                table: "OutboxDispatchControls",
                columns: new[] { "Id", "Paused" },
                values: new object[] { 1, false });

            migrationBuilder.CreateIndex(
                name: "IX_Idempotency_Status_LeaseUntil",
                schema: "reliability",
                table: "IdempotencyRecords",
                columns: new[] { "Status", "LeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "UX_Idempotency_Operation_Owner_Key",
                schema: "reliability",
                table: "IdempotencyRecords",
                columns: new[] { "Operation", "OwnerScopeHash", "KeyHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Inbox_Status_LeaseUntil",
                schema: "reliability",
                table: "InboxMessages",
                columns: new[] { "Status", "LeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Status_LeaseUntil",
                schema: "reliability",
                table: "OutboxMessages",
                columns: new[] { "Status", "LeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_Outbox_Status_NextAttemptAt",
                schema: "reliability",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailDeliveryReceipts",
                schema: "reliability");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords",
                schema: "reliability");

            migrationBuilder.DropTable(
                name: "InboxMessages",
                schema: "reliability");

            migrationBuilder.DropTable(
                name: "OutboxDispatchControls",
                schema: "reliability");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "reliability");

            migrationBuilder.DropTable(
                name: "OutboxOperations",
                schema: "reliability");

            migrationBuilder.DropTable(
                name: "OutboxRecoveries",
                schema: "reliability");
        }
    }
}
