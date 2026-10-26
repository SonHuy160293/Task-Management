namespace DevFlow.EntityFrameworkCore.Reliability;

public enum OutboxStatus : short { Pending = 0, Processing = 1, Published = 2, DeadLetter = 3 }
public enum InboxStatus : short { Processing = 0, Processed = 1, DeadLetter = 2 }
public enum IdempotencyStatus : short { InProgress = 0, Completed = 1 }
public enum ReliabilityDatabaseProvider { SqlServer, PostgreSql }
