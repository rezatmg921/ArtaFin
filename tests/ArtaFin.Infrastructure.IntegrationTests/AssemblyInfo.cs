using Xunit;

// Tests share one disposable SQL Server database. Serial execution prevents
// concurrent test cases from migrating or deleting the database simultaneously.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
