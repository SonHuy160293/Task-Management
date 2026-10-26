# Identity Worker
Executable composition root for the durable outbox and notification receipts.
Own-module Application/Infrastructure references are permitted. Never reference Notification internals.
No registration endpoint or email provider belongs here. Log only message IDs and bounded failure codes.
