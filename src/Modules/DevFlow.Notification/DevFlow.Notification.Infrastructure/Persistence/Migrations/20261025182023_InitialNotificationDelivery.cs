using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DevFlow.Notification.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialNotificationDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notification");

            migrationBuilder.EnsureSchema(
                name: "reliability");

            migrationBuilder.CreateTable(
                name: "EmailDeliveries",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestJson = table.Column<string>(type: "text", nullable: false),
                    FrozenRequestJson = table.Column<string>(type: "text", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    BudgetReserved = table.Column<bool>(type: "boolean", nullable: false),
                    FirstAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProviderId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDeliveries", x => x.Id);
                    table.CheckConstraint("CkEmailDeliveryAttempts", "\"Attempts\" >= 0");
                    table.CheckConstraint("CkEmailDeliveryStatus", "\"Status\" IN ('Pending','Processing','ProviderAccepted','DeadLetter','Expired','Suppressed')");
                    table.CheckConstraint("CkEmailDeliveryTime", "\"ExpiresAt\" > \"CreatedAt\"");
                });

            migrationBuilder.CreateTable(
                name: "EmailDeliveryControls",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    PausedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PauseReason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ConsecutiveFailures = table.Column<int>(type: "integer", nullable: false),
                    NextRequestAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Day = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    DailyReservations = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    MonthlyReservations = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDeliveryControls", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailOperations",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Operator = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailSuppressions",
                schema: "notification",
                columns: table => new
                {
                    RecipientHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSuppressions", x => x.RecipientHash);
                });

            migrationBuilder.CreateTable(
                name: "EmailWebhookReceipts",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecipientHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReceivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailWebhookReceipts", x => x.Id);
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
                name: "EmailDeliveryAttempts",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailDeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailDeliveryAttempts_EmailDeliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalSchema: "notification",
                        principalTable: "EmailDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "notification",
                table: "EmailDeliveryControls",
                columns: new[] { "Id", "ConsecutiveFailures", "DailyReservations", "Day", "Month", "MonthlyReservations", "NextRequestAt", "PauseReason", "PausedUntil" },
                values: new object[] { 1, 0, 0, "", "", 0, new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null });

            migrationBuilder.InsertData(
                schema: "reliability",
                table: "OutboxDispatchControls",
                columns: new[] { "Id", "Paused" },
                values: new object[] { 1, false });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_ProviderId",
                schema: "notification",
                table: "EmailDeliveries",
                column: "ProviderId",
                unique: true,
                filter: "\"ProviderId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_Status_LeaseUntil",
                schema: "notification",
                table: "EmailDeliveries",
                columns: new[] { "Status", "LeaseUntil" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveries_Status_NextAttemptAt",
                schema: "notification",
                table: "EmailDeliveries",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmailDeliveryAttempts_DeliveryId_Attempt",
                schema: "notification",
                table: "EmailDeliveryAttempts",
                columns: new[] { "DeliveryId", "Attempt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailWebhookReceipts_EventId",
                schema: "notification",
                table: "EmailWebhookReceipts",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmailWebhookReceipts_ProcessedAt_ProviderId",
                schema: "notification",
                table: "EmailWebhookReceipts",
                columns: new[] { "ProcessedAt", "ProviderId" });

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
                name: "EmailDeliveryAttempts",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "EmailDeliveryControls",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "EmailOperations",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "EmailSuppressions",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "EmailWebhookReceipts",
                schema: "notification");

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
                name: "EmailDeliveries",
                schema: "notification");
        }
    }
}
