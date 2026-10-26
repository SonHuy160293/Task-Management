using DevFlow.Notification.Infrastructure;
using DevFlow.Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddNotificationPersistence(builder.Configuration);
    using var host = builder.Build();
    using var scope = host.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.MigrateAsync();
    Console.WriteLine("Notification schema is up to date.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Notification migration failed ({ex.GetType().Name}).");
    Environment.ExitCode = 1;
}
