using System;
using System.Linq;
using System.Linq.Expressions;
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

        Assert.Single(extracted);
        Assert.Equal("x_TotalPeople", extracted.First().Key);
        Assert.Equal(expression.Body, extracted.First().Value.First());
    }

    [Fact]
    public void ExtractMemberExpressionInMethod()
    {
        // Calling a service using EF fields
        Expression<Func<Person, AgeService, int>> expression = (person, ager) => ager.GetAge(person.Birthday);
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);

        Assert.Single(extracted);
        Assert.Equal("person_Birthday", extracted.First().Key);
        Assert.Equal(((MethodCallExpression)expression.Body).Arguments[0], extracted.First().Value.First());
    }

    [Fact]
    public void ExtractLongMemberExpressionSameNameInMethod()
    {
        // Calling a service using EF fields
        Expression<Func<Project, ConfigService, string>> expression = (p, srv) => srv.Get(p.Name, p.Children.FirstOrDefault().Name);
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);

        Assert.Equal(2, extracted.Count);
        Assert.Equal("p_Name", extracted.First().Key);
        Assert.Equal(((MethodCallExpression)expression.Body).Arguments[0], extracted.First().Value.First());
        Assert.Equal("p_Children_FirstOrDefault___Name", extracted.ElementAt(1).Key);
        Assert.Equal(((MethodCallExpression)expression.Body).Arguments[1], extracted.ElementAt(1).Value.First());
    }

    [Fact]
    public void ExtractExpressionInAsync()
    {
        // Calling a service using EF fields
        Expression<Func<Person, AgeService, int>> expression = (ctx, srv) => srv.GetAgeAsync(ctx.Birthday).GetAwaiter().GetResult();
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);

        Assert.Single(extracted);
        Assert.Equal("ctx_Birthday", extracted.First().Key);
        Assert.Equal(((MethodCallExpression)((MethodCallExpression)((MethodCallExpression)expression.Body).Object).Object).Arguments[0], extracted.First().Value.First());
    }
    [Fact]
    public void ExtractExpressionConditional()
    {
        // Calling a service using EF fields
        Expression<Func<Project, AgeService, DateTime>> expression = (project, ageSrv) => project.Updated == null ? DateTime.MinValue : new DateTime(ageSrv.GetAgeAsync(project.Updated).Result);
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);

        Assert.Single(extracted);
        Assert.Equal("project_Updated", extracted.First().Key);
        Assert.Equal(2, extracted.First().Value.Count);
        Assert.Equal(((BinaryExpression)((ConditionalExpression)expression.Body).Test).Left, extracted.First().Value.First());
        Assert.Equal(((MethodCallExpression)((MemberExpression)((UnaryExpression)((NewExpression)((ConditionalExpression)expression.Body).IfFalse).Arguments[0]).Operand).Expression).Arguments[0], extracted.First().Value.ElementAt(1));
    }
    [Fact]
    public void ExtractExpressionNullableType()
    {
        // Calling a service using EF fields
        Expression<Func<User, TestDataContext, Person>> expression = (user, ctx) => user.RelationId.HasValue ? ctx.People.FirstOrDefault(u => u.Id == user.RelationId.Value) : null;
        var extractor = new ExpressionExtractor();
        var extracted = extractor.Extract(expression.Body, expression.Parameters[0], false);

        Assert.Single(extracted);
        Assert.Equal("user_RelationId", extracted.First().Key);
        Assert.Equal(2, extracted.First().Value.Count);
        Assert.Equal(((MemberExpression)((ConditionalExpression)expression.Body).Test).Expression, extracted.First().Value.First());
        Assert.Equal(((MemberExpression)((BinaryExpression)((LambdaExpression)((MethodCallExpression)((ConditionalExpression)expression.Body).IfTrue).Arguments[1]).Body).Right).Expression, extracted.First().Value.ElementAt(1));
    }
}