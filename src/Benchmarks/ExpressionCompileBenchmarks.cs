using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;

namespace Benchmarks;

[MemoryDiagnoser]
public class ExpressionCompileBenchmarks
{
    private readonly LambdaExpression lambda;
    private readonly object?[] args;

    public ExpressionCompileBenchmarks()
    {
        (lambda, args) = BuildLambda();
    }

    [Benchmark]
    public Delegate Compile()
    {
        return lambda.Compile();
    }

    [Benchmark]
    public Delegate CompileNoInterpretation()
    {
        return lambda.Compile(preferInterpretation: false);
    }

    [Benchmark]
    public Delegate CompilePreferInterpretation()
    {
        return lambda.Compile(preferInterpretation: true);
    }

    [Benchmark]
    public object? CompileAndDynamicInvoke()
    {
        return lambda.Compile().DynamicInvoke(args);
    }

    [Benchmark]
    public object? CompileAndDynamicInvokePreferInterpretation()
    {
        return lambda.Compile(preferInterpretation: true).DynamicInvoke(args);
    }

    private static (LambdaExpression lambda, object?[] args) BuildLambda()
    {
        var ctxParam = Expression.Parameter(typeof(MovieContext), "ctx");
        var yearFloorParam = Expression.Parameter(typeof(int), "yearFloor");
        var actorIdFloorParam = Expression.Parameter(typeof(int), "actorIdFloor");
        var takeParam = Expression.Parameter(typeof(int), "take");
        var includeDirectorParam = Expression.Parameter(typeof(bool), "includeDirector");
        var titleSuffixParam = Expression.Parameter(typeof(string), "titleSuffix");

        var movieExpr = Expression.Property(ctxParam, nameof(MovieContext.Movie));
        var actorsExpr = Expression.Property(movieExpr, nameof(Movie.Actors));
        var actorParam = Expression.Parameter(typeof(Actor), "actor");

        var actorIdExpr = Expression.Property(actorParam, nameof(Actor.Id));
        var actorNameExpr = Expression.Property(actorParam, nameof(Actor.Name));
        var actorRolesExpr = Expression.Property(actorParam, nameof(Actor.Roles));
        var roleParam = Expression.Parameter(typeof(string), "role");

        var filteredActorsExpr = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Where),
            [typeof(Actor)],
            actorsExpr,
            Expression.Lambda<Func<Actor, bool>>(Expression.GreaterThan(actorIdExpr, actorIdFloorParam), actorParam)
        );

        var actorRoleSummaryExpr = Expression.Call(typeof(string), nameof(string.Join), Type.EmptyTypes, Expression.Constant("|"), actorRolesExpr);

        var actorProjectionExpr = Expression.MemberInit(
            Expression.New(typeof(ActorProjection)),
            Expression.Bind(typeof(ActorProjection).GetProperty(nameof(ActorProjection.Id))!, actorIdExpr),
            Expression.Bind(typeof(ActorProjection).GetProperty(nameof(ActorProjection.Name))!, actorNameExpr),
            Expression.Bind(typeof(ActorProjection).GetProperty(nameof(ActorProjection.RoleSummary))!, actorRoleSummaryExpr),
            Expression.Bind(
                typeof(ActorProjection).GetProperty(nameof(ActorProjection.PrimaryRole))!,
                Expression.Call(typeof(Enumerable), nameof(Enumerable.FirstOrDefault), [typeof(string)], actorRolesExpr)
            ),
            Expression.Bind(
                typeof(ActorProjection).GetProperty(nameof(ActorProjection.TopRoles))!,
                Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.ToList),
                    [typeof(string)],
                    Expression.Call(typeof(Enumerable), nameof(Enumerable.Take), [typeof(string)], actorRolesExpr, Expression.Constant(2))
                )
            ),
            Expression.Bind(
                typeof(ActorProjection).GetProperty(nameof(ActorProjection.MatchingRoles))!,
                Expression.Call(
                    typeof(Enumerable),
                    nameof(Enumerable.Count),
                    [typeof(string)],
                    actorRolesExpr,
                    Expression.Lambda<Func<string, bool>>(Expression.GreaterThan(Expression.Property(roleParam, nameof(string.Length)), Expression.Constant(4)), roleParam)
                )
            )
        );

        var actorSelectExpr = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Select),
            [typeof(Actor), typeof(ActorProjection)],
            filteredActorsExpr,
            Expression.Lambda<Func<Actor, ActorProjection>>(actorProjectionExpr, actorParam)
        );

        var actorListExpr = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.ToList),
            [typeof(ActorProjection)],
            Expression.Call(typeof(Enumerable), nameof(Enumerable.Take), [typeof(ActorProjection)], actorSelectExpr, takeParam)
        );

        var directorExpr = Expression.Property(movieExpr, nameof(Movie.Director));
        var directorProjectionExpr = Expression.Condition(
            Expression.AndAlso(includeDirectorParam, Expression.NotEqual(directorExpr, Expression.Constant(null, typeof(Person)))),
            Expression.MemberInit(
                Expression.New(typeof(PersonProjection)),
                Expression.Bind(typeof(PersonProjection).GetProperty(nameof(PersonProjection.Id))!, Expression.Property(directorExpr, nameof(Person.Id))),
                Expression.Bind(typeof(PersonProjection).GetProperty(nameof(PersonProjection.Name))!, Expression.Property(directorExpr, nameof(Person.Name))),
                Expression.Bind(typeof(PersonProjection).GetProperty(nameof(PersonProjection.BirthYear))!, Expression.Property(directorExpr, nameof(Person.BirthYear)))
            ),
            Expression.Constant(null, typeof(PersonProjection))
        );

        var titleExpr = Expression.Call(typeof(string).GetMethod(nameof(string.Concat), [typeof(string), typeof(string)])!, Expression.Property(movieExpr, nameof(Movie.Title)), titleSuffixParam);

        var body = Expression.MemberInit(
            Expression.New(typeof(MovieProjection)),
            Expression.Bind(typeof(MovieProjection).GetProperty(nameof(MovieProjection.Id))!, Expression.Property(movieExpr, nameof(Movie.Id))),
            Expression.Bind(typeof(MovieProjection).GetProperty(nameof(MovieProjection.Title))!, titleExpr),
            Expression.Bind(typeof(MovieProjection).GetProperty(nameof(MovieProjection.IsRecent))!, Expression.GreaterThanOrEqual(Expression.Property(movieExpr, nameof(Movie.Year)), yearFloorParam)),
            Expression.Bind(typeof(MovieProjection).GetProperty(nameof(MovieProjection.ActorCount))!, Expression.Property(actorsExpr, nameof(List<Actor>.Count))),
            Expression.Bind(typeof(MovieProjection).GetProperty(nameof(MovieProjection.Actors))!, actorListExpr),
            Expression.Bind(typeof(MovieProjection).GetProperty(nameof(MovieProjection.Director))!, directorProjectionExpr)
        );

        var lambda = Expression.Lambda(body, ctxParam, yearFloorParam, actorIdFloorParam, takeParam, includeDirectorParam, titleSuffixParam);

        var args = new object?[] { CreateContext(), 2000, 2, 3, true, " [extended]" };

        return (lambda, args);
    }

    private static MovieContext CreateContext()
    {
        return new MovieContext
        {
            Movie = new Movie
            {
                Id = 7,
                Title = "Arrival",
                Year = 2016,
                Director = new Person
                {
                    Id = 11,
                    Name = "Denis Villeneuve",
                    BirthYear = 1967,
                },
                Actors =
                [
                    new Actor
                    {
                        Id = 1,
                        Name = "Amy Adams",
                        Roles = ["Louise Banks", "Lead"],
                    },
                    new Actor
                    {
                        Id = 2,
                        Name = "Jeremy Renner",
                        Roles = ["Ian Donnelly", "Lead"],
                    },
                    new Actor
                    {
                        Id = 3,
                        Name = "Forest Whitaker",
                        Roles = ["Colonel Weber", "Support"],
                    },
                    new Actor
                    {
                        Id = 4,
                        Name = "Michael Stuhlbarg",
                        Roles = ["Agent Halpern", "Support"],
                    },
                ],
            },
        };
    }

    public sealed class MovieContext
    {
        public required Movie Movie { get; init; }
    }

    public sealed class Movie
    {
        public int Id { get; init; }
        public required string Title { get; init; }
        public int Year { get; init; }
        public Person? Director { get; init; }
        public required List<Actor> Actors { get; init; }
    }

    public sealed class Person
    {
        public int Id { get; init; }
        public required string Name { get; init; }
        public int BirthYear { get; init; }
    }

    public sealed class Actor
    {
        public int Id { get; init; }
        public required string Name { get; init; }
        public required List<string> Roles { get; init; }
    }

    public sealed class MovieProjection
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public bool IsRecent { get; set; }
        public int ActorCount { get; set; }
        public required List<ActorProjection> Actors { get; set; }
        public PersonProjection? Director { get; set; }
    }

    public sealed class ActorProjection
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string RoleSummary { get; set; }
        public string? PrimaryRole { get; set; }
        public required List<string> TopRoles { get; set; }
        public int MatchingRoles { get; set; }
    }

    public sealed class PersonProjection
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int BirthYear { get; set; }
    }
}
