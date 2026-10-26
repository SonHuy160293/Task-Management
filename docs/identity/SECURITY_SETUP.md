# Identity registration security setup

These components and the Identity host support ID-001. They do not provision
PostgreSQL or by themselves mark any backlog endpoint Completed.
Local Redis deployment and environment templates are available in
[deploy/README.md](../../deploy/README.md#local-endpoints).
The independent [Identity Docker deployment](../../deploy/README.md#startup-implementation)
now provisions PostgreSQL 18, runs the [migration runner](MIGRATIONS.md), and starts
the API after successful migrations. The runner does not register these security
services or seed any data.
Application owns the contracts under `DevFlow.Identity.Application/Abstractions/Security`;
Infrastructure implements them under `DevFlow.Identity.Infrastructure/Security`.

## Register and configure

The Identity composition root calls AddInfrastructure, which calls
`services.AddIdentitySecurity(configuration)`.
The extension validates options on host startup.
Defaults are shown below; secrets must come from environment variables, development
user secrets, or the deployment secret store, never committed configuration.

```json
{
  "Identity": {
    "Security": {
      "Argon2": {
        "MemoryKiB": 65536,
        "Iterations": 3,
        "Parallelism": 1,
        "MaxConcurrency": 2
      },
      "VerificationToken": { "Lifetime": "1.00:00:00" },
      "RegistrationRateLimit": {
        "IpPermitLimit": 20,
        "IpWindow": "00:10:00",
        "EmailPermitLimit": 5,
        "EmailWindow": "01:00:00"
      }
    }
  }
}
```

Supply `Identity__Security__RegistrationRateLimit__RedisConnectionString` and
`Identity__Security__RegistrationRateLimit__KeySecretBase64`. Generate the latter
from 32 random bytes using `RandomNumberGenerator`, Base64-encoded. All replicas
must share the same secret and Redis logical database. Rotating the key starts new
counter buckets; coordinate rotation and do not rotate it on every startup.
Use authenticated TLS connections for production Redis. An existing singleton
`IConnectionMultiplexer` can be registered before `AddIdentitySecurity` to reuse
the host's Redis connection; configure bounded timeouts and fail-fast backlog policy.

Use a dedicated Redis capacity/eviction policy that preserves counters for their
TTL. Eviction or data loss resets limits. The limiter uses atomic single-key Lua
scripts, compatible with Redis Cluster; no schema or EF migration is involved.
It uses fixed windows starting with the first attempt. Rejections do not extend
the window. IP and email buckets are independent; an IP rejection does not consume
an email permit. A successful admission is not refunded after a later failure.

## API integration

For a proxied deployment, configure trusted-forwarding services for the actual
proxy topology, then preserve this middleware order:

```csharp
app.UseForwardedHeaders(); // Only known/trusted proxies may supply forwarding headers.
// Existing global exception/response and correlation middleware.
app.UseIdentityRegistrationProtection();
// Existing authentication, authorization, and controller mapping.
```

The protection middleware only guards `POST /api/v1/auth/register` (case-insensitive,
optional trailing slash). It uses `Connection.RemoteIpAddress`,
checks the IP bucket before reading a body, caps bodies at 16 KiB including chunked
requests, parses the email, then checks the email bucket. Duplicate email JSON
properties are rejected to avoid ambiguity with model binding. The original body
is replayed unchanged to MVC. Invalid email/JSON returns 400, oversized body 413,
exhausted quota 429 plus `Retry-After` seconds, and missing client IP/unavailable
Redis 503. Rejections contain no account-dependent details. Also configure server
request-body timeouts/minimum data rates for slow-client protection.

After this guard, the registration validator validates the DTO fields, including
password length. The handler checks the local common-password list before hashing
or querying account existence. A match reaches centralized failure handling as
HTTP 400 with the message `Password is too common.` Redis rate-limiter failures
can independently produce 503. See the
[registration failure-path notes](API_CONVENTIONS.md#existing-registration-failure-path).

Controller actions pass `CancellationToken` to `ISender.SendAsync`; validators,
handlers, repositories, and every asynchronous dependency propagate that token.
Preserve validation -> logging -> performance -> tracing -> transaction ordering.

## Passwords

`IPasswordHasher` creates Argon2id v19 PHC strings with independently random 16-byte
salts and 32-byte digests. Store the full PHC string in the existing
`PasswordCredential.PasswordHash`. Verification uses a fixed-time comparison and
returns `SuccessRehashNeeded` for weaker memory/iteration costs. No rehash is
persisted here. PHC parsing is bounded to 512 characters, 128 MiB, six iterations,
four lanes, a 16-byte salt, and a 32-byte digest before derivation. Unsupported or
malformed hashes fail verification. Coordinate these verification ceilings before
importing hashes from another system or increasing configured costs.

The singleton hasher limits active derivations to two by default (up to 256 MiB of
Argon2 memory for two accepted 128 MiB verification hashes, plus runtime overhead).
Cancellation interrupts admission to the semaphore. The selected managed library
cannot interrupt a running derivation: the semaphore slot is retained until it
finishes, then cancellation is propagated. Password byte arrays are cleared after
use; immutable .NET strings cannot be reliably erased. Benchmark latency, memory,
and concurrency on deployment hardware before tuning these defaults.

The password policy accepts 15-128 Unicode scalar values and rejects malformed
UTF-16; it preserves whitespace and normalization, without composition rules.
Both registration and future login/reset implementations must use the same encoding
and normalization convention.

Registration checks seven common passwords locally, using a case-insensitive
comparison of the entire password without trimming it. This deliberately small
list provides limited coverage: a password absent from it may still be common or
compromised. There is no external breach lookup or general strength score.
Passwords and credential material must not appear in logs.

## Verification tokens and persistence boundary

`IVerificationTokenGenerator` uses 32 CSPRNG bytes encoded as a 43-character
Base64url bearer token. `TokenHash` is SHA-256 of the UTF-8 **encoded bearer string**,
not of the original random bytes. ID-002 must hash the submitted string identically.
The generated purpose is `verify_email`; timestamps use the injected `TimeProvider`
and the default lifetime is 24 hours. `ToString()` redacts token-bearing objects.

Use the digest and expiry with the existing `OneTimeToken.Create`; persist the user,
credential, and challenge in the real Unit of Work transaction. Never persist or
return the raw token in an API response. Registration encrypts the raw token for Notification and stages the request in the same transaction. The generator itself does not consume tokens or send mail. Single-use consumption belongs to ID-002. The
provisioned case-insensitive unique email index remains an operational prerequisite.

## Focused verification

```powershell
dotnet test tests/DevFlow.Identity.Tests/DevFlow.Identity.Tests.csproj
powershell -File scripts/Test-Architecture.ps1
```

Redis integration tests are explicitly skipped unless `DEVFLOW_TEST_REDIS` points
to an isolated test Redis instance. They create only expiring, randomly keyed test
counters; never point them at production Redis. With that variable set, the same
test command checks concurrent requests across two independent connections, email
case normalization, IPv4-mapped address normalization, expiry, and store shutdown.
Password-policy and registration HTTP tests exercise the local blocklist. The
logging privacy tests check that registration credentials are excluded from logs.

References: [OWASP password storage](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html),
[NIST password guidance](https://pages.nist.gov/800-63-4/sp800-63b.html).

## Verification email key

The Identity API requires `Identity:VerificationEmail:KeyId` and `Identity:VerificationEmail:PublicKeyFile`. The ID uses 1–64 ASCII letters, digits or hyphens; the file must contain an RSA public PEM key of at least 3072 bits. The key is loaded at startup, and invalid configuration fails startup with a sanitized error. Distribute the matching Notification public key to Identity before switching IDs and restart the API to rotate. Identity never needs a Notification private key. The outbox and account commit together; delivery can be repeated after failures.
