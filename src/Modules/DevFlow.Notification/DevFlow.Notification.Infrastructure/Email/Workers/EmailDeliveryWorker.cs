using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevFlow.Messaging.Outbox;
using DevFlow.Notification.Contracts;
using DevFlow.Notification.Domain.Models;
using DevFlow.Notification.Infrastructure.Email.Providers;
using DevFlow.Notification.Infrastructure.Email.Templates;
using DevFlow.Notification.Infrastructure.Persistence;
using DevFlow.Notification.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevFlow.Notification.Infrastructure.Email.Workers;
public sealed class EmailDeliveryWorker(IServiceScopeFactory scopes, ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("Email delivery scan failed ({ExceptionType})", ex.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<EmailDeliveryOptions>>().Value;
        await NotificationMetrics.ScanAsync(db, ct);
        await ProcessReceiptsAsync(db, ct);
        if (options.Mode == "Disabled")
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        EmailDelivery? job;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(271003)", ct);
            var control = await db.EmailDeliveryControls.SingleAsync(ct);
            if (control.PausedUntil > now || control.NextRequestAt > now)
            {
                return;
            }

            job = await db.EmailDeliveries.FromSqlInterpolated($"""
                SELECT * FROM notification."EmailDeliveries"
                WHERE ("Status" = 'Pending' AND "NextAttemptAt" <= {now})
                   OR ("Status" = 'Processing' AND "LeaseUntil" <= {now})
                ORDER BY "CreatedAt" LIMIT 1 FOR UPDATE SKIP LOCKED
                """).SingleOrDefaultAsync(ct);
            if (job is null)
            {
                return;
            }

            if (!job.CanSend(now))
            {
                job.Expire();
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return;
            }

            string day = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string month = now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            if (control.Day != day)
            {
                control.Day = day;
                control.DailyReservations = 0;
            }

            if (control.Month != month)
            {
                control.Month = month;
                control.MonthlyReservations = 0;
            }

            // Reserve each submission, including retries. This deliberately underuses the
            // provider allowance rather than exceeding a new UTC day's/month's budget.
            if (control.DailyReservations >= options.DailyLimit || control.MonthlyReservations >= options.MonthlyLimit)
            {
                return;
            }

            control.DailyReservations++;
            control.MonthlyReservations++;
            job.ReserveBudget();
            control.NextRequestAt = now.AddSeconds(1);
            job.Claim(now);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        var protection = scope.ServiceProvider.GetRequiredService<EmailPayloadProtection>();
        EmailDeliveryAttempt? attempt = null;
        try
        {
            byte[] raw;
            if (job.FrozenRequestJson is null)
            {
                var request = JsonSerializer.Deserialize<EmailDeliveryRequestedV1>(job.RequestJson)!;
                raw = protection.Decrypt(request);
                try
                {
                    var rendered = VerificationTemplate.Render(request, raw, options);
                    string recipientHash = protection.RecipientHash(rendered.To[0]);
                    if (await db.EmailSuppressions.AnyAsync(x => x.RecipientHash == recipientHash, ct))
                    {
                        job.Suppress();
                        await db.SaveChangesAsync(ct);
                        return;
                    }

                    byte[] frozen = JsonSerializer.SerializeToUtf8Bytes(rendered, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    try
                    {
                        job.Freeze(protection.Freeze(job.Id, frozen));
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(frozen);
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(raw);
                }
            }

            raw = protection.Thaw(job.Id, job.FrozenRequestJson!);
            try
            {
                var rendered = JsonSerializer.Deserialize<RenderedEmail>(raw, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
                string recipientHash = protection.RecipientHash(rendered.To[0]);
                if (await db.EmailSuppressions.AnyAsync(x => x.RecipientHash == recipientHash, ct))
                {
                    job.Suppress();
                    await db.SaveChangesAsync(ct);
                    return;
                }

                if (!job.CanSend(DateTimeOffset.UtcNow))
                {
                    job.Expire();
                    await db.SaveChangesAsync(ct);
                    return;
                }

                if (job.LeaseUntil <= DateTimeOffset.UtcNow.AddSeconds(10))
                {
                    return;
                }

                job.BeginAttempt(DateTimeOffset.UtcNow);
                attempt = new EmailDeliveryAttempt
                {
                    DeliveryId = job.Id,
                    Attempt = job.Attempts,
                    StartedAt = DateTimeOffset.UtcNow
                };
                db.EmailDeliveryAttempts.Add(attempt);
                await db.SaveChangesAsync(ct);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(10));
                ProviderResult result;
                try
                {
                    result = await scope.ServiceProvider.GetRequiredService<IEmailProvider>().SendAsync(job.Id, Encoding.UTF8.GetString(raw), deadline.Token);
                }
                catch (OperationCanceledException)when (!ct.IsCancellationRequested)
                {
                    result = new("Retry", Code: "ProviderTimeout");
                }
                catch (HttpRequestException)
                {
                    result = new("Retry", Code: "ProviderNetwork");
                }
                catch (System.Net.Mail.SmtpException)
                {
                    result = new("Retry", Code: "ProviderNetwork");
                }

                await CompleteAsync(db, job, attempt, result, ct);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(raw);
            }
        }
        catch (OperationCanceledException)when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Email job lease changed: {MessageId}", job.Id);
        }
        catch (Exception ex)when (ex is IOException or UnauthorizedAccessException)
        {
            // A missing/rotating key is operational, not permission to discard a recoverable job.
            await CompleteAsync(db, job, attempt, new("Paused", Code: "EncryptionUnavailable"), ct);
        }
        catch (Exception ex)when (ex is JsonException or ArgumentException or CryptographicException or FormatException)
        {
            await CompleteAsync(db, job, attempt, new("Permanent", Code: "InvalidTemplatePayload"), ct);
        }
    }

    private static async Task CompleteAsync(NotificationDbContext db, EmailDelivery job, EmailDeliveryAttempt? attempt, ProviderResult result, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(271003)", ct);
        var control = await db.EmailDeliveryControls.SingleAsync(ct);
        await db.Entry(control).ReloadAsync(ct);
        if (result.Status == "Accepted")
        {
            job.Accept(result.ProviderId!);
            control.ConsecutiveFailures = 0;
        }
        else if (result.Status == "Permanent")
        {
            job.DeadLetter(result.Code!);
        }
        else
        {
            control.ConsecutiveFailures++;
            if (result.Status == "Paused")
            {
                control.PausedUntil = DateTimeOffset.MaxValue;
                control.PauseReason = result.Code;
            }
            else if (control.ConsecutiveFailures >= 5)
            {
                control.PausedUntil = DateTimeOffset.UtcNow.AddMinutes(1);
                control.PauseReason = "CircuitOpen";
            }

            if (!job.CanSend(DateTimeOffset.UtcNow))
            {
                job.Expire();
            }
            else
            {
                job.Retry(DateTimeOffset.UtcNow.Add(result.RetryAfter is { } delay && delay > TimeSpan.Zero ? delay : RetrySchedule.Delay(job.Attempts, 3600)), result.Code!);
            }
        }

        if (attempt is not null)
        {
            attempt.Outcome = result.Status;
            attempt.FailureCode = result.Code;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private static async Task ProcessReceiptsAsync(NotificationDbContext db, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var receipts = await db.EmailWebhookReceipts.FromSqlRaw("""
            SELECT * FROM notification."EmailWebhookReceipts" WHERE "ProcessedAt" IS NULL
            ORDER BY "ReceivedAt" LIMIT 50 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        foreach (var receipt in receipts)
        {
            var job = await db.EmailDeliveries.SingleOrDefaultAsync(x => x.ProviderId == receipt.ProviderId, ct);
            if (job is null)
            {
                continue;
            }

            string? outcome = receipt.EventType switch
            {
                "email.delivered" => "Delivered",
                "email.bounced" => "Bounced",
                "email.complained" => "Complained",
                "email.failed" => "Failed",
                _ => null,
            };
            if (outcome is not null)
            {
                job.RecordOutcome(outcome);
            }

            if (outcome is "Bounced" or "Complained" && receipt.RecipientHash is not null)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO notification."EmailSuppressions" ("RecipientHash", "Reason", "CreatedAt")
                    VALUES ({receipt.RecipientHash}, {outcome}, {DateTimeOffset.UtcNow})
                    ON CONFLICT ("RecipientHash") DO NOTHING
                    """, ct);
            }

            receipt.ProcessedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }
}
