using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using EntityGraphQL.Compiler.Util;
using Xunit;
using static EntityGraphQL.Tests.ServiceFieldTests;

namespace EntityGraphQL.Tests.Util;

public class ExpressionExtractorTests
{
    [Fact]
    public void ExtractMemberExpression()
    {
        Expression<Func<TestDataContext, int>> expression = x => x.TotalPeople;
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);
        Assert.NotNull(extracted);
        Assert.Single(extracted);
        Assert.Equal("egql__x_TotalPeople", extracted.First().Key);
        Assert.Equal(expression.Body, extracted.First().Value.First());
    }

    [Fact]
    public void ExtractMemberExpressionInMethod()
    {
        // Calling a service using EF fields
        Expression<Func<Person, AgeService, int>> expression = (person, ager) => ager.GetAge(person.Birthday);
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);
        Assert.NotNull(extracted);
        Assert.Single(extracted);
        Assert.Equal("egql__person_Birthday", extracted.First().Key);
        Assert.Equal(((MethodCallExpression)expression.Body).Arguments[0], extracted.First().Value.First());
    }

    [Fact]
    public void ExtractLongMemberExpressionSameNameInMethod()
    {
        // Calling a service using EF fields
        Expression<Func<Project, ConfigService, string>> expression = (p, srv) => srv.Get(p.Name, p.Children.First().Name);
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);
        Assert.NotNull(extracted);
        Assert.Equal(2, extracted.Count);
        Assert.Equal("egql__p_Name", extracted.First().Key);
        Assert.Equal(((MethodCallExpression)expression.Body).Arguments[0], extracted.First().Value.First());
        Assert.Equal("egql__p_Children_First___Name", extracted.ElementAt(1).Key);
        Assert.Equal(((MethodCallExpression)expression.Body).Arguments[1], extracted.ElementAt(1).Value.First());
    }

    [Fact]
    public void ExtractObjectBuiltFromMultipleMembersInMethodArg()
    {
        // Building an object from 2 context members as an argument to the service call - the construction is not a
        // leaf, so each member read is extracted on its own rather than both being credited to the construction.
        Expression<Func<User, ConfigService, ProjectConfig>> expression = (user, srv) => srv.Get(new Project { Id = user.Id, Name = user.Field2 });
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);
        Assert.NotNull(extracted);
        Assert.Equal(2, extracted.Count);
        var init = (MemberInitExpression)((MethodCallExpression)expression.Body).Arguments[0];
        Assert.Equal("egql__user_Id", extracted.First().Key);
        Assert.Equal(((MemberAssignment)init.Bindings[0]).Expression, extracted.First().Value.Single());
        Assert.Equal("egql__user_Field2", extracted.ElementAt(1).Key);
        Assert.Equal(((MemberAssignment)init.Bindings[1]).Expression, extracted.ElementAt(1).Value.Single());
    }

    [Fact]
    public void ExtractConditionalInMethodArg()
    {
        // No VisitConditional override - the ifTrue/ifFalse reads are both credited to the conditional, so that one
        // node is extracted twice under the one name. ExpressionReplacer tolerates that repeat by design.
        // (the test is a binary, which VisitBinary walks into, so user.Id is also extracted on its own)
        Expression<Func<User, ConfigService, ProjectConfig>> expression = (user, srv) => srv.Get(user.Id > 1 ? user.Id : user.Field2.Length);
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);
        Assert.NotNull(extracted);
        Assert.Equal(2, extracted.Count);
        Assert.Equal("egql__user_Id", extracted.First().Key);
        var arg = ((MethodCallExpression)expression.Body).Arguments[0];
        Assert.Equal([arg, arg], extracted.ElementAt(1).Value);
    }

    [Fact]
    public void ExtractExpressionInAsync()
    {
        // Calling a service using EF fields
        Expression<Func<Person, AgeService, Task<int>>> expression = (ctx, srv) => srv.GetAgeAsync(ctx.Birthday);
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);
        Assert.NotNull(extracted);
        Assert.Single(extracted);
        Assert.Equal("egql__ctx_Birthday", extracted.First().Key);
        Assert.Equal(((MethodCallExpression)expression.Body).Arguments[0], extracted.First().Value.First());
    }

    [Fact]
    public void ExtractExpressionConditional()
    {
        // Calling a service using EF fields
        Expression<Func<Project, AgeService, DateTime>> expression = (project, ageSrv) => project.Updated == null ? DateTime.MinValue : new DateTime(ageSrv.GetAgeAsync(project.Updated).Result);
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);
        Assert.NotNull(extracted);
        Assert.Single(extracted);
        Assert.Equal("egql__project_Updated", extracted.First().Key);
        Assert.Equal(2, extracted.First().Value.Count);
        Assert.Equal(((BinaryExpression)((ConditionalExpression)expression.Body).Test).Left, extracted.First().Value.First());
        Assert.Equal(
            ((MethodCallExpression)((MemberExpression)((UnaryExpression)((NewExpression)((ConditionalExpression)expression.Body).IfFalse).Arguments[0]).Operand).Expression!).Arguments[0],
            extracted.First().Value.ElementAt(1)
        );
    }

    [Fact]
    public void ExtractExpressionNullableType()
    {
        // Calling a service using EF fields
        Expression<Func<User, TestDataContext, Person?>> expression = (user, ctx) => user.RelationId.HasValue ? ctx.People.FirstOrDefault(u => u.Id == user.RelationId.Value) : null;
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);
        Assert.NotNull(extracted);
        Assert.Single(extracted);
        Assert.Equal("egql__user_RelationId", extracted.First().Key);
        Assert.Equal(2, extracted.First().Value.Count);
        Assert.Equal(((MemberExpression)((ConditionalExpression)expression.Body).Test).Expression, extracted.First().Value.First());
        Assert.Equal(
            ((MemberExpression)((BinaryExpression)((LambdaExpression)((MethodCallExpression)((ConditionalExpression)expression.Body).IfTrue).Arguments[1]).Body).Right).Expression,
            extracted.First().Value.ElementAt(1)
        );
    }
}
