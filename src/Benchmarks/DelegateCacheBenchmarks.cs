using System.Linq;
using BenchmarkDotNet.Attributes;
using EntityGraphQL;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;

namespace Benchmarks
{    
    public class DelegateCacheBenchmarks : BaseBenchmark
    {      
        [Benchmark]
        public void Query_SingleObjectWithArg_NoCache()
        {
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    movie(id: ""077b3041-307a-42ba-9ffe-1121fcfc918b"") {
                        id name released
                        director {
                            id name dob
                        }
                        actors {
                            id name dob
                        }
                    }
                }"
            }, new ExecutionOptions
            {
                EnableQueryCache = false,
                EnableDelegateCache = false,
#if DEBUG
                NoExecution = true
#endif
            });
        }


        [Benchmark]
        public void Query_SingleObjectWithArg_QueryCache()
        {
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    movie(id: ""077b3041-307a-42ba-9ffe-1121fcfc918b"") {
                        id name released
                        director {
                            id name dob
                        }
                        actors {
                            id name dob
                        }
                    }
                }"
            }, new ExecutionOptions
            {
                EnableQueryCache = true,
                EnableDelegateCache = false,
#if DEBUG
                NoExecution = true
#endif
            });
        }

        [Benchmark]
        public void Query_SingleObjectWithArg_DelegateCache()
        {
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    movie(id: ""077b3041-307a-42ba-9ffe-1121fcfc918b"") {
                        id name released
                        director {
                            id name dob
                        }
                        actors {
                            id name dob
                        }
                    }
                }"
            }, new ExecutionOptions
            {
                EnableQueryCache = false,
                EnableDelegateCache = true,
#if DEBUG
                NoExecution = true
#endif
            });
        }

        [Benchmark]
        public void Query_SingleObjectWithArg_AllCache()
        {
            RunQuery(GetContext(), new QueryRequest
            {
                Query = @"{
                    movie(id: ""077b3041-307a-42ba-9ffe-1121fcfc918b"") {
                        id name released
                        director {
                            id name dob
                        }
                        actors {
                            id name dob
                        }
                    }
                }"
            }, new ExecutionOptions
            {
                EnableQueryCache = true,
                EnableDelegateCache = true,
#if DEBUG
                NoExecution = true
#endif
            });
        }

    }
}