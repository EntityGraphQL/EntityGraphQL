using Xunit;

#if DEBUG
[assembly: CollectionBehavior(DisableTestParallelization = false)]
#else
[assembly: CollectionBehavior(DisableTestParallelization = true)]
#endif