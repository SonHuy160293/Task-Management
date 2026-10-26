# Put a YARP gateway in front of independently deployed APIs

Status: accepted, 2026-10-27. Extends ADR 0004 without changing module persistence,
migrations, or messaging ownership.

Frontend requests need one public HTTP interface while developers and module
integrations still need direct access. We choose a standalone .NET 10 YARP host,
module-prefixed explicit public routes, and operator-managed TLS ingress. This
fits the existing independent Compose projects and centrally pinned YARP package
without coupling the gateway to module assemblies or introducing service discovery.

Only registration and the signed provider callback are initially public. Explicit
routes require a deliberate change when publishing a capability; broad module
catch-alls would also expose Identity's internal integration interface. Backend
authentication and response contracts remain authoritative. Forwarding does not
constitute authentication, and CORS does not restrict non-browser callers.

The platform owns a separate HTTP network with unique API aliases. Production
APIs publish no host ports; local overrides retain loopback access. A stable,
configurable gateway IP allows APIs to trust a single forwarding peer rather than
every container. Subnet collision checks are the operational cost of that choice.
Ingress peer IPs must match what the gateway actually observes after Docker NAT.

Readiness covers the gateway's own usable configuration, so a failed module does
not remove healthy routes from ingress. Deadlines and body limits bound requests;
POSTs are never retried automatically. Invalid route-file updates retain a validated
snapshot, including malformed JSON updates. The gateway adds a shared availability
dependency; the existing single-host template does not provide high availability.

See [gateway operations](../gateway/README.md) for configuration, verification,
public URLs, and rollout/rollback.
