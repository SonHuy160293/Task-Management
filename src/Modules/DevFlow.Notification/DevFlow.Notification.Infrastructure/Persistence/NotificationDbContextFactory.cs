using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevFlow.Notification.Infrastructure.Persistence;
public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__NotificationDb") ?? "Host=localhost;Database=DevFlowNotification;Username=devflow_notification").Options);
}
