# HTTP integration ownership

Outbound HTTP integration infrastructure lives in the existing DevFlow.AspNetCore
BuildingBlock under HttpClient/. Each type has its own file, grouped by responsibility.
No separate production HTTP client project is required.

Application owns destination service interfaces and depends on the integration
result contracts in SharedKernel. Infrastructure implements destination adapters
using IIntegrationHttpClient<TDestination>; the generic marker binds each adapter
to its configured named transport. API composition roots register adapters with
AddIntegrationHttpClient<TService, TImplementation>(destinationName, section).

Registration uses IHttpClientFactory, replaces host-default resilience handlers
with one destination-specific standard pipeline, and retains configuration keys
and mutation retry restrictions. ASP.NET Core framework references supply HTTP,
configuration, and options APIs; the resilience package stays centrally versioned.

Requests use injected transport instance methods. DI registration helpers remain
static extensions. Application references to DevFlow.AspNetCore are rejected by
the architecture check.
