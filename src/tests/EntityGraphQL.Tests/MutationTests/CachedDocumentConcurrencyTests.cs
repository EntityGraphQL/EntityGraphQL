using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// With EnableQueryCache (default on) the same parsed GraphQLDocument instance is shared across requests.
/// Executing it must not write per-request state onto the shared AST nodes - these tests run the same
/// document concurrently and check for cross-request contamination.
/// </summary>
public class CachedDocumentConcurrencyTests
{
    private const int Threads = 24;
    private const int Rounds = 100;

    private class ListMutations
    {
        [GraphQLMutation]
        public static List<Person> AddPerson(TestDataContext db, string name)
        {
            var person = new Person { Name = name, LastName = name };
            db.People.Add(person);
            return db.People.ToList();
        }

        [GraphQLMutation]
        public static System.Linq.Expressions.Expression<System.Func<TestDataContext, IEnumerable<Person>>> AddPersonExp(TestDataContext db, string name)
        {
            db.People.Add(new Person { Name = name, LastName = name });
            return ctx => ctx.People;
        }
    }

    private static async System.Threading.Tasks.Task RunConcurrent(string query, string fieldName)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddMutationsFrom<ListMutations>();

        for (var round = 0; round < Rounds; round++)
        {
            // gate so all requests execute the shared document as close together as possible
            var gate = new System.Threading.Tasks.TaskCompletionSource();
            var tasks = Enumerable
                .Range(0, Threads)
                .Select(i =>
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        await gate.Task;
                        var name = $"person_{round}_{i}";
                        var data = new TestDataContext { People = [] };
                        var result = await schema.ExecuteRequestWithContextAsync(
                            new QueryRequest
                            {
                                Query = query,
                                Variables = new QueryVariables { { "name", name } },
                            },
                            data,
                            null,
                            null
                        );
                        return (name, result);
                    })
                )
                .ToList();
            gate.SetResult();

            var results = await System.Threading.Tasks.Task.WhenAll(tasks);

            foreach (var (name, result) in results)
            {
                Assert.Null(result.Errors);
                dynamic people = result.Data![fieldName]!;
                Assert.Equal(1, Enumerable.Count(people));
                // wrong-data check: the result must be THIS request's person
                Assert.Equal(name, people[0].name);
            }
        }
    }

    public class ValueService
    {
        public int GetValue(int id) => id * 10;
    }

    public class GatedAnimals : IEnumerable<Animal>
    {
        private readonly List<Animal> inner;
        public System.Threading.SemaphoreSlim Gate { get; } = new(0);
        public System.Threading.SemaphoreSlim Reached { get; } = new(0);
        public bool Block { get; set; }

        public GatedAnimals(List<Animal> inner) => this.inner = inner;

        public IEnumerator<Animal> GetEnumerator()
        {
            if (Block)
            {
                Block = false; // only block the first enumeration (pass 1)
                Reached.Release();
                Gate.Wait();
            }
            return inner.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class RaceContext
    {
        public IEnumerable<Animal> Animals { get; set; } = new List<Animal>();
        public List<Cat> Cats { get; set; } = [];
        public List<Dog> Dogs { get; set; } = [];
    }

    [Fact]
    public async System.Threading.Tasks.Task InterleavedRequests_DifferentIncludeValues_DoNotContaminatePassTwoTypes()
    {
        // PossibleNextContextTypes is written to the shared cached AST node during each request's first pass
        // and read during its second pass. Interleave two requests whose @include variable differs so their
        // expanded selections (and therefore those types) differ:
        //   A compiles pass 1 (writes types incl. Cat) -> A blocks executing pass 1
        //   B runs completely with $inc = false (overwrites the node's types without Cat)
        //   A resumes -> pass 2 must still project A's Cat fields
        var schema = new SchemaProvider<RaceContext>();
        schema.AddInterface<Animal>("Animal", "An animal").AddAllFields();
        schema.AddType<Cat>("Cat", "A cat").ImplementAllBaseTypes().AddAllFields();
        schema.AddType<Dog>("Dog", "A dog").ImplementAllBaseTypes().AddAllFields();
        schema.Query().AddField(ctx => ctx.Animals, "All animals");
        schema.Type<Animal>().AddField("svcValue", "service field to force two-pass execution").Resolve<ValueService>((a, srv) => srv.GetValue(a.Id));

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(new ValueService());
        var sp = serviceCollection.BuildServiceProvider();

        var query =
            @"query Q($inc: Boolean!) {
                animals {
                    name
                    svcValue
                    ... on Cat { lives @include(if: $inc) }
                    ... on Dog { hasBone }
                }
            }";

        var gatedA = new GatedAnimals(
            [
                new Cat
                {
                    Id = 1,
                    Name = "Felix",
                    Lives = 9,
                },
                new Dog
                {
                    Id = 2,
                    Name = "Rex",
                    HasBone = true,
                },
            ]
        )
        {
            Block = true,
        };
        var ctxA = new RaceContext { Animals = gatedA };

        // request A ($inc: true) - will block while executing pass 1
        var taskA = System.Threading.Tasks.Task.Run(() =>
            schema.ExecuteRequestWithContextAsync(
                new QueryRequest
                {
                    Query = query,
                    Variables = new QueryVariables { { "inc", true } },
                },
                ctxA,
                sp,
                null
            )
        );
        if (!await gatedA.Reached.WaitAsync(5000))
        {
            var earlyResult = await taskA;
            Assert.Fail($"request A never reached pass 1 execution. Result errors: {string.Join("; ", earlyResult.Errors?.Select(e => e.Message) ?? ["none - completed without enumerating"])}");
        }

        // request B ($inc: false) - same cached document, runs to completion while A is paused
        var ctxB = new RaceContext
        {
            Animals = new List<Animal>
            {
                new Dog
                {
                    Id = 3,
                    Name = "Fido",
                    HasBone = false,
                },
            },
        };
        var resultB = await schema.ExecuteRequestWithContextAsync(
            new QueryRequest
            {
                Query = query,
                Variables = new QueryVariables { { "inc", false } },
            },
            ctxB,
            sp,
            null
        );
        Assert.Null(resultB.Errors);

        // let A finish - its second pass must use ITS types, not B's
        gatedA.Gate.Release();
        var resultA = await taskA;

        Assert.True(resultA.Errors == null, $"request A failed: {string.Join("; ", resultA.Errors?.Select(e => e.Message) ?? [])}");
        dynamic animals = resultA.Data!["animals"]!;
        Assert.Equal(2, Enumerable.Count(animals));
        var cat = ((IEnumerable<object>)animals).Cast<dynamic>().First(a => a.name == "Felix");
        Assert.Equal(9, cat.lives); // lost if pass 2 used request B's types (no Cat fragment)
        Assert.Equal(10, cat.svcValue);
    }

    [Fact]
    public System.Threading.Tasks.Task ConcurrentMutations_ListResult_NoCrossRequestContamination()
    {
        // list-returning mutation - execution previously wrote RootParameter/ListExpression onto the cached node
        return RunConcurrent(@"mutation M($name: String!) { addPerson(name: $name) { name lastName height projects { id name } manager { name } } }", "addPerson");
    }

    [Fact]
    public System.Threading.Tasks.Task ConcurrentMutations_ExpressionListResult_NoCrossRequestContamination()
    {
        return RunConcurrent(@"mutation M($name: String!) { addPersonExp(name: $name) { name lastName height projects { id name } manager { name } } }", "addPersonExp");
    }
}
