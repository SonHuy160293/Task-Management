# Notification module
Root and Modules AGENTS.md apply. Notification owns delivery, templates, provider outcomes and suppression, not account verification.
Workers are executable composition roots referencing only their own Application/Infrastructure, Contracts and BuildingBlocks.
Use the notification schema for module data and reliability for technical records in the Notification database.
Never log recipients, bodies, links, encryption envelopes, provider secrets or raw callbacks. Consumers acknowledge only after the inbox/job transaction commits.
