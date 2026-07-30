using System.Linq.Expressions;
using EntityGraphQL.Extensions;
using EntityGraphQL.Schema;
using EntityGraphQL.Tests.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EntityGraphQL.EF.Tests;

/// <summary>
/// A list field resolved from the query context itself - the pattern for a child collection that is not a
/// navigation property:
/// <code>
/// schema.Type&lt;Comment&gt;().AddField("replies", "...")
///     .Resolve&lt;MyDbContext&gt;((c, db) => db.Comments.Where(r => r.ParentId == c.Id).OrderBy(r => r.Created));
/// </code>
/// That field is a database query, so nothing in its selection may be one of EntityGraphQL's own helper
/// methods - a provider has no translation for them. It goes unnoticed until another field of the same kind is
/// selected below it, because only then does the helper sit inside a query the provider must translate rather
/// than being the outermost call, which we invoke ourselves. EF Core then fails the whole operation with
/// "The LINQ expression 'p_X => new Dynamic_y{...}' could not be translated".
///
/// These assert on the expression handed to the provider rather than on the result, because what any one
/// provider can execute is a separate question from what we generate: the translatable form of a nested
/// collection selection is a correlated sub-select, which SQLite cannot run at all (it has no APPLY).
/// </summary>
public class NestedServiceQueryableFieldTests
{
    private class MovieService
    {
        public IQueryable<Movie> GetMovies(TestDbContext db, int actorId) => db.Movies.Where(m => m.Actors.Any(a => a.Id == actorId));
    }

    private static (SchemaProvider<TestDbContext> schema, TestDbContext context, TestDbContextFactory factory, ServiceProvider services) Build()
    {
        var factory = new TestDbContextFactory();
        var context = factory.CreateContext();
        var movie = new Movie("Movie 1") { Released = new DateTime(2000, 1, 1) };
        movie.Actors.Add(new Actor("Actor 1") { Birthday = new DateTime(1980, 1, 1) });
        context.Movies.Add(movie);
        context.SaveChanges();

        var services = new ServiceCollection();
        services.AddSingleton(context);
        services.AddSingleton(new MovieService());
        return (SchemaBuilder.FromObject<TestDbContext>(), context, factory, services.BuildServiceProvider());
    }

    private static (QueryResult result, List<string> queryableHelperCalls) Execute(SchemaProvider<TestDbContext> schema, TestDbContext context, ServiceProvider services, string query)
    {
        // our own extension methods applied to an IQueryable - what the provider is asked to translate
        List<string> helperCalls = [];
        var options = new ExecutionOptions
        {
            BeforeExecuting = (Expression expression, bool isFinal) =>
            {
                helperCalls.AddRange(AssertExpression.CallsTo(typeof(QueryableExtensions), expression));
                return expression;
            },
        };
        return (schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, context, services, null, options), helperCalls);
    }

    private static void AddOtherActorsFromContext(SchemaProvider<TestDbContext> schema) =>
        schema.Type<Actor>().AddField("otherActors", "Actors via the db context").Resolve<TestDbContext>((a, db) => db.Actors.Where(x => x.Id >= a.Id).OrderBy(x => x.Name));

    private static (QueryResult result, List<string> helperCalls, List<string> queryableCalls) ExecuteCollectingAll(
        SchemaProvider<TestDbContext> schema,
        TestDbContext context,
        ServiceProvider services,
        string query
    )
    {
        List<string> helperCalls = [];
        List<string> queryableCalls = [];
        var options = new ExecutionOptions
        {
            BeforeExecuting = (Expression expression, bool isFinal) =>
            {
                helperCalls.AddRange(AssertExpression.CallsTo(typeof(QueryableExtensions), expression));
                queryableCalls.AddRange(AssertExpression.CallsTo(typeof(Queryable), expression));
                return expression;
            },
        };
        return (schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, context, services, null, options), helperCalls, queryableCalls);
    }

    private static void AddMoviesFromContext(SchemaProvider<TestDbContext> schema) =>
        schema
            .Type<Actor>()
            .AddField("moviesFromDb", "Movies via the db context")
            .Resolve<TestDbContext>((a, db) => db.Movies.Where(m => m.Actors.Any(x => x.Id == a.Id)).OrderBy(m => m.Released));

    /// <summary>
    /// The reported failure: one context-resolved list field selected below another. The inner projection is
    /// composed into the outer query, so it has to be plain LINQ.
    /// </summary>
    [Fact]
    public void ContextResolvedListField_NestedInsideAnother_StaysTranslatable()
    {
        var (schema, context, factory, services) = Build();
        using var _ = factory;
        AddMoviesFromContext(schema);
        schema.Type<Actor>().AddField("otherActors", "Actors via the db context").Resolve<TestDbContext>((a, db) => db.Actors.Where(x => x.Id > a.Id).OrderBy(x => x.Name));

        var (_, helperCalls) = Execute(schema, context, services, "{ actors { id otherActors { id moviesFromDb { id name } } } }");

        Assert.Empty(helperCalls);
    }

    /// <summary>
    /// One level of it executes as well as translating - the selection becomes part of the query.
    /// </summary>
    [Fact]
    public void ContextResolvedListField_StaysTranslatableAndExecutes()
    {
        var (schema, context, factory, services) = Build();
        using var _ = factory;
        AddMoviesFromContext(schema);

        var (res, helperCalls) = Execute(schema, context, services, "{ actors { id moviesFromDb { id name __typename } } }");

        Assert.Null(res.Errors);
        Assert.Empty(helperCalls);
        dynamic actors = res.Data!["actors"]!;
        Assert.Equal("Movie 1", actors[0].moviesFromDb[0].name);
    }

    /// <summary>
    /// A service that is not the query context is client-side and can return null, so its projection keeps the
    /// null check - there is no provider to translate that one for.
    /// </summary>
    [Fact]
    public void ListFieldFromOtherService_KeepsNullCheck()
    {
        var (schema, context, factory, services) = Build();
        using var _ = factory;
        schema.Type<Actor>().AddField("moviesFromService", "Movies via a service").Resolve<MovieService, TestDbContext>((a, srv, db) => srv.GetMovies(db, a.Id));

        var (res, helperCalls) = Execute(schema, context, services, "{ actors { id moviesFromService { id name } } }");

        Assert.Null(res.Errors);
        Assert.Equal([nameof(QueryableExtensions.SelectWithNullCheck)], helperCalls);
        dynamic actors = res.Data!["actors"]!;
        Assert.Equal("Movie 1", actors[0].moviesFromService[0].name);
    }

    /// <summary>
    /// The nested case translates but SQLite cannot execute the correlated collection sub-select it produces
    /// (no APPLY). Kept for anyone running these against a provider that supports it - SQL Server, Postgres.
    /// </summary>
    [Fact(Skip = "Correlated collection sub-select requires SQL APPLY, which SQLite does not support")]
    public void ContextResolvedListField_NestedInsideAnother_Executes()
    {
        var (schema, context, factory, services) = Build();
        using var _ = factory;
        AddMoviesFromContext(schema);
        schema.Type<Actor>().AddField("otherActors", "Actors via the db context").Resolve<TestDbContext>((a, db) => db.Actors.Where(x => x.Id >= a.Id).OrderBy(x => x.Name));

        var (res, _) = Execute(schema, context, services, "{ actors { id otherActors { id moviesFromDb { id name } } } }");

        Assert.Null(res.Errors);
        dynamic actors = res.Data!["actors"]!;
        Assert.Equal("Movie 1", actors[0].otherActors[0].moviesFromDb[0].name);
    }

    /// <summary>
    /// The single-object equivalent: a context-resolved object field nested inside a context-resolved list
    /// field. It used to be projected with ProjectWithNullCheck, which no provider can translate. The
    /// selection is projected inside the query - Where().Select().FirstOrDefault() - as it is for a plain
    /// context field like <c>movie(id: 1)</c>, so the sub-select is evaluated once rather than repeated per
    /// selected field.
    /// </summary>
    [Fact]
    public void ContextResolvedObjectField_NestedInsideListField_StaysTranslatable()
    {
        var (schema, context, factory, services) = Build();
        using var _ = factory;
        AddOtherActorsFromContext(schema);
        AddFirstMovieFromContext(schema);

        var (_, helperCalls, queryableCalls) = ExecuteCollectingAll(schema, context, services, "{ actors { id otherActors { id firstMovie { id name } } } }");

        Assert.Empty(helperCalls);
        // projected inside the query, so the single-item call appears once - not once per selected field
        Assert.Single(queryableCalls, c => c == nameof(Queryable.FirstOrDefault));
    }

    /// <summary>
    /// As above - projecting an object out of a correlated sub-select needs APPLY, which SQLite does not have.
    /// Kept for anyone running these against SQL Server or Postgres.
    /// </summary>
    [Fact(Skip = "Projecting an object from a correlated sub-select requires SQL APPLY, which SQLite does not support")]
    public void ContextResolvedObjectField_NestedInsideListField_Executes()
    {
        var (schema, context, factory, services) = Build();
        using var _ = factory;
        AddOtherActorsFromContext(schema);
        AddFirstMovieFromContext(schema);

        var (res, _) = Execute(schema, context, services, "{ actors { id otherActors { id firstMovie { id name } } } }");

        Assert.Null(res.Errors);
        dynamic actors = res.Data!["actors"]!;
        Assert.Equal("Movie 1", actors[0].otherActors[0].firstMovie.name);
    }

    private static void AddFirstMovieFromContext(SchemaProvider<TestDbContext> schema) =>
        schema
            .Type<Actor>()
            .AddField("firstMovie", "First movie via the db context")
            .Resolve<TestDbContext>((a, db) => db.Movies.Where(m => m.Actors.Any(x => x.Id == a.Id)).OrderBy(m => m.Id).FirstOrDefault());

}
