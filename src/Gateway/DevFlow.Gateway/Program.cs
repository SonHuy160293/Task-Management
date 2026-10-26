using DevFlow.Gateway;

try
{
    var builder = WebApplication.CreateBuilder(args);
    await using var app = GatewayApplication.Build(builder);
    await app.RunAsync();
    return 0;
}
catch (Exception)
{
    // Startup exceptions can contain configuration values. Emit a static diagnostic only.
    Console.Error.WriteLine("Gateway startup failed. Validate routing, CORS, host, and trusted ingress configuration.");
    return 1;
}
