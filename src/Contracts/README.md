# Cross-module contracts

These projects contain types shared across module boundaries. They do not contain
module domain models, persistence, or API-local request models.

- `DevFlow.Identity.Contracts/Users/`: the user DTO returned by Identity's
  integration API and consumed by Tasks.
- `DevFlow.Notification.Contracts/EmailDelivery/V1/`: the version 1 encrypted
  email request, its payload, and the durable acceptance receipt used between
  Identity and Notification.

Each public type has its own file. Group new contracts by owning module and
capability; group versioned messages by their protocol version. The existing types
retain their original namespaces despite the folders: MassTransit message identities
include the type namespace, and changing those identities can break communication
with existing workers or queued messages. Treat public type names, namespaces,
serialized fields, and event identifiers as compatibility-sensitive.
