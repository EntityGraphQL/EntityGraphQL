using System;
using System.Linq;
using System.Linq.Expressions;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using EntityGraphQL.Tests.Util;
using Xunit;

namespace EntityGraphQL.Tests;

public class ExecutionOptionsTests
{
    [Fact]
    public void TestBeforeExecution()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        var gql = new QueryRequest
        {
            Query =
                @"{
                    project(id: 99) {
                        name
                        tasks {
                            name id
                        }
                    }
                }",
        };

        var result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { });
        Assert.Null(result.Errors);

        dynamic project = result.Data!["project"]!;
        Assert.Equal(5, Enumerable.Count(project.tasks));

        result = schema.ExecuteRequestWithContext(gql, data, null, null, new ExecutionOptions { BeforeExecuting = (e, isFinal) => Expression.Constant(null) });
        Assert.Null(result.Errors);

        project = result.Data!["project"]!;
        Assert.Null(project);
    }

    [Theory]
    // List field
    [InlineData(
        @"{ projects {
                    name
                    tasks { name id }
                } }",
        "projects",
        1,
        1
    )]
    // scalar field
    [InlineData(@"{ totalPeople }", "totalPeople", 1, 1)]
    // ObjectProjection field - will be called 2 times because the expression built is mainProject.TagWith() == null ? null : new { name = mainProject.TagWith().name }
    [InlineData(@"{ mainProject { name } }", "mainProject", 1, 2)]
    // ListToSingle field
    [InlineData(@"{ project(id: 99) { name } }", "project", 1, 1)]
    public void TestBeforeExpressionBuild(string query, string fieldName, int expectedCalledInExp, int expectedBeforeExpressionBuildCalled)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("projectsPaged", ctx => ctx.Projects, "Paged projects").UseConnectionPaging();
        schema.Query().AddField("projectItems", ctx => ctx.Projects, "Paged projects").UseOffsetPaging();
        var data = new TestDataContext();
        FillProjectData(data);

        var gql = new QueryRequest { Query = query };

        var calledInExp = 0;
        var beforeExpressionBuildCalled = 0;
        var result = schema.ExecuteRequestWithContext(
            gql,
            data,
            null,
            null,
            new ExecutionOptions
            {
                BeforeRootFieldExpressionBuild = (e, op, field) =>
                {
                    Assert.Equal(fieldName, field);
                    beforeExpressionBuildCalled++;
                    Action onCalled = () => calledInExp++;
                    return Expression.Call(typeof(TestTagWith), nameof(TestTagWith.TagWith), [e.Type], e, Expression.Constant(onCalled));
                },
            }
        );
        Assert.Null(result.Errors);
        Assert.Equal(expectedCalledInExp, beforeExpressionBuildCalled);
        Assert.Equal(expectedBeforeExpressionBuildCalled, calledInExp);
    }

    [Fact]
    public void TestBeforeExpressionBuildScalar() =>
        TestBeforeExpressionBuildExpression(
            "{ totalPeople }",
            // p_TestDataContext.TotalPeople.TagWith(value(System.Action))
            AssertExpression.Call(null, "TagWith", AssertExpression.AnyOfType(typeof(int)), AssertExpression.AnyOfType(typeof(Action))),
            "totalPeople",
            null
        );

    [Fact]
    public void TestBeforeExpressionBuildOffsetPaging() =>
        TestBeforeExpressionBuildExpression(
            "query MyOp { projectItems { items { name } } }",
            AssertExpression.Conditional(
                AssertExpression.Any(),
                AssertExpression.Any(),
                AssertExpression.MemberInit(
                    [
                        AssertExpression.MemberBinding(
                            "items",
                            AssertExpression.Call(
                                null,
                                nameof(Enumerable.ToList),
                                AssertExpression.Call(
                                    null,
                                    "Select",
                                    AssertExpression.Call(null, "TagWith", AssertExpression.Any(), AssertExpression.AnyOfType(typeof(Action))),
                                    AssertExpression.Any()
                                )
                            )
                        ),
                    ]
                )
            ),
            "projectItems",
            "MyOp"
        );

    [Fact]
    public void TestBeforeExpressionBuildConnectionPaging() =>
        TestBeforeExpressionBuildExpression(
            "query ConnectionOp { projectsPaged { edges { node { name } } } }",
            AssertExpression.Conditional(
                AssertExpression.Any(),
                AssertExpression.Any(),
                // new {edges = ctx.Projects.Skip(GetSkipNumber(arg_ConnectionArgs_exec))
                // .Take(GetTakeNumber(arg_ConnectionArgs_exec))
                // .Select(edgeNode => new ConnectionEdge`1() {Node = new {name = edgeNode.Name}})
                // .Select((newEdgeParam, cursor_idx) => new ConnectionEdge`1() {Node = newEdgeParam.Node, Cursor = GetCursor(arg_ConnectionArgs_exec, cursor_idx)})
                // .Select(newEdgeParam => new {node = newEdgeParam.Node})
                // .ToListWithNullCheck(True)})
                AssertExpression.MemberInit(
                    [
                        AssertExpression.MemberBinding(
                            "edges",
                            AssertExpression.Call(
                                null,
                                nameof(Enumerable.ToList),
                                AssertExpression.Call(
                                    null,
                                    "Select",
                                    AssertExpression.Call(
                                        null,
                                        "Select",
                                        AssertExpression.Call(
                                            null,
                                            "Select",
                                            AssertExpression.Call(null, "TagWith", AssertExpression.Any(), AssertExpression.AnyOfType(typeof(Action))),
                                            AssertExpression.Any()
                                        ),
                                        AssertExpression.Any()
                                    ),
                                    AssertExpression.Any()
                                )
                            )
                        ),
                    ]
                )
            ),
            "projectsPaged",
            "ConnectionOp"
        );

    private void TestBeforeExpressionBuildExpression(string query, AssertExpression expectedExpression, string fieldName, string? opName)
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("projectsPaged", ctx => ctx.Projects, "Paged projects").UseConnectionPaging();
        schema.Query().AddField("projectItems", ctx => ctx.Projects, "Paged projects").UseOffsetPaging();
        var data = new TestDataContext();
        FillProjectData(data);

        var gql = new QueryRequest { Query = query };

        var calledInExp = 0;
        var beforeExpressionBuildCalled = 0;
        var result = schema.ExecuteRequestWithContext(
            gql,
            data,
            null,
            null,
            new ExecutionOptions
            {
                BeforeRootFieldExpressionBuild = (e, op, field) =>
                {
                    Assert.Equal(fieldName, field);
                    Assert.Equal(opName, op);
                    beforeExpressionBuildCalled++;
                    Action onCalled = () => calledInExp++;
                    return Expression.Call(typeof(TestTagWith), nameof(TestTagWith.TagWith), [e.Type], e, Expression.Constant(onCalled));
                },
                BeforeExecuting = (e, isFinal) =>
                {
                    AssertExpression.Matches(expectedExpression, e);
                    return e;
                },
            }
        );
        Assert.Null(result.Errors);
    }

    [Fact]
    public void TestBeforeExpressionBuildInvalid()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        var data = new TestDataContext();
        FillProjectData(data);

        var gql = new QueryRequest { Query = @"{ projects { name tasks { name id } } }" };

        var result = schema.ExecuteRequestWithContext(
            gql,
            data,
            null,
            null,
            new ExecutionOptions
            {
                BeforeRootFieldExpressionBuild = (e, op, field) =>
                {
                    // we changed the return type
                    return Expression.Constant(7);
                },
            }
        );
        Assert.NotNull(result.Errors);
        Assert.Equal("Field 'projects' - BeforeExpressionBuild changed the return type from System.Collections.Generic.List`1[EntityGraphQL.Tests.Project] to System.Int32", result.Errors[0].Message);
    }

    private static void FillProjectData(TestDataContext data)
    {
        data.Projects =
        [
            new Project
            {
                Id = 99,
                Name = "Project 1",
                Tasks =
                [
                    new Task { Id = 0, Name = "Task 1" },
                    new Task { Id = 1, Name = "Task 2" },
                    new Task { Id = 2, Name = "Task 3" },
                    new Task { Id = 3, Name = "Task 4" },
                    new Task { Id = 4, Name = "Task 5" },
                ],
            },
        ];
    }
}

public static class TestTagWith
{
    public static T TagWith<T>(this T field, Action called)
    {
        called();
        return field;
    }
}
