using DevFlow.Identity.Application.Abstractions.Messaging;
using DevFlow.Identity.Application.DependencyInjection;
using DevFlow.Identity.Application.Features.Messaging;
using DevFlow.Identity.Infrastructure.Messaging;
using DevFlow.Identity.Infrastructure.Persistence.Core;
using DevFlow.Identity.Infrastructure.Persistence.DbContexts;
using DevFlow.SharedKernel.Abstractions.Message;
using DevFlow.SharedKernel.Abstractions.Persistence;
using DevFlow.SharedKernel.Abstractions.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DevFlow.Notification.Tests;

public sealed class IdentityWorkerCompositionTests
{
    [Fact]
    public void ReceiptOnlyWorkerValidatesWithoutRegistrationSecurityServices()
    {
        var registrations = new ServiceCollection();
        registrations.AddLogging();
        registrations.AddEmailReceiptApplication();
        registrations.AddDbContext<IdentityDbContext>(x => x.UseNpgsql("Host=localhost;Database=unused;Username=unused"));
        registrations.AddScoped<IUnitOfWork, IdentityUnitOfWork>();
        registrations.AddScoped<IEmailDeliveryReceiptStore, EmailDeliveryReceiptStore>();
        using var services = registrations.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        using var scope = services.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IRequestHandler<RecordEmailDeliveryReceiptCommand, Result<bool>>>());
        Assert.Single(registrations.Where(x => x.ServiceType.IsGenericType && x.ServiceType.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));
    }
}
