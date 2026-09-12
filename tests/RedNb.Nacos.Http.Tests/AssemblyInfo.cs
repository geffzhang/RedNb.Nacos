using Xunit;

// Each HTTP fixture hosts its own server; bound test concurrency across machines.
[assembly: CollectionBehavior(MaxParallelThreads = 4)]
