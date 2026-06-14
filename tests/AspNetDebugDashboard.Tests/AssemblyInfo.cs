using Xunit;

// LiteDB's BsonMapper.Global is shared static state. Running the storage tests
// in parallel can race its lazy member registration ("Member Timestamp not
// found on BsonMapper"), so the suite runs serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
