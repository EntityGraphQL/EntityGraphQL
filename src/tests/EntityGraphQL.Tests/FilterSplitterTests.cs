using System;
using System.Linq.Expressions;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema.FieldExtensions;
using Xunit;

namespace EntityGraphQL.Tests;

/// <summary>
/// Tests for the FilterSplitter functionality, which separates service-dependent
/// filter expressions from regular database-queryable expressions for two-pass execution.
/// </summary>
public class FilterSplitterTests
{
    private readonly FilterSplitter splitter = new(typeof(Person));

    [Fact]
    public void SplitFilter_NoServiceFields_ReturnsOnlyNonServiceFilter()
    {
        // Arrange: filter with only regular fields
        Expression<Func<Person, bool>> filter = p => p.Name == "John" && p.Id > 5;

        // Act
        var result = splitter.SplitFilter(filter);

        // Assert
        Assert.NotNull(result.NonServiceFilter);
        Assert.Null(result.ServiceFilter);
    }

    [Fact]
    public void SplitFilter_OrWithOnlyRegularFields_KeepsInNonService()
    {
        // Arrange: OR condition with only regular fields
        Expression<Func<Person, bool>> filter = p => p.Name == "John" || p.Name == "Jane";

        // Act
        var result = splitter.SplitFilter(filter);

        // Assert
        Assert.NotNull(result.NonServiceFilter);
        Assert.Null(result.ServiceFilter);
    }

    [Fact]
    public void SplitFilter_NotExpressionWithRegularField_KeepsInNonService()
    {
        // Arrange: NOT expression with only regular fields
        Expression<Func<Person, bool>> filter = p => !(p.Name == "John") && p.Id > 1;

        // Act
        var result = splitter.SplitFilter(filter);

        // Assert
        Assert.NotNull(result.NonServiceFilter);
        Assert.Null(result.ServiceFilter);
    }

    [Fact]
    public void SplitFilter_ComplexAndConditions_WithRegularFieldsOnly()
    {
        // Arrange: complex AND conditions with only regular fields
        Expression<Func<Person, bool>> filter = p => p.Id > 1 && p.Name == "John" && p.LastName != null && p.LastName.Length > 2;

        // Act
        var result = splitter.SplitFilter(filter);

        // Assert
        Assert.NotNull(result.NonServiceFilter);
        Assert.Null(result.ServiceFilter);
    }

    [Fact]
    public void SplitFilter_VariousComplexLogicalExpressions_DoesNotThrow()
    {
        // Test various complex logical combinations to ensure robustness
        var testCases = new[]
        {
            // Simple cases
            p => p.Name == "John",
            p => p.Id > 5 && p.Name == "John",
            p => p.Name == "John" || p.Name == "Jane",
            // Complex nested conditions
            p => p.Name == "John" && (p.Id > 5 || p.LastName == "Doe"),
            p => (p.Id > 1 && p.Name == "John") || (p.LastName == "Smith" && p.Name == "Jane"),
            // NOT expressions
            p => !(p.Name == "John") && p.Id > 1,
            p => !(p.Id < 5 || p.Name == "John"),
            // Deeply nested with service fields
            (Expression<Func<Person, bool>>)(p => p.Id > 1 && (p.Name == "John" || p.Name == "Jane") && p.LastName != null && (p.LastName.Length > 2 || p.LastName == "Doe")),
        };

        foreach (var testCase in testCases)
        {
            // Act & Assert - should not throw and should return a result
            var result = splitter.SplitFilter(testCase);
            Assert.NotNull(result);
        }
    }

    [Fact]
    public void SplitFilter_OnlyServiceFields_ReturnsOnlyServiceFilter()
    {
        // Arrange: Create a filter expression with only service fields
        Expression<Func<Person, bool>> filter = p => ServiceExpressionMarker.MarkService(p.Id) > 21;

        // Act
        var result = splitter.SplitFilter(filter);

        // Assert
        Assert.Null(result.NonServiceFilter);
        Assert.NotNull(result.ServiceFilter);
    }

    [Fact]
    public void SplitFilter_MixedAndFields_SplitsCorrectly()
    {
        // Arrange: Create a filter with both regular and service fields connected by AND
        Expression<Func<Person, bool>> filter = p => p.Name == "John" && ServiceExpressionMarker.MarkService(p.Id) > 21;

        // Act
        var result = splitter.SplitFilter(filter);

        // Assert - should split into both service and non-service parts
        Assert.NotNull(result.NonServiceFilter);
        Assert.NotNull(result.ServiceFilter);

        // Assert that NonServiceFilter contains only non-service fields
        var nonServiceMarkerCheck = new ServiceMarkerCheckVisitor();
        nonServiceMarkerCheck.Visit(result.NonServiceFilter.Body);
        Assert.False(nonServiceMarkerCheck.ContainsServiceMarker, "NonServiceFilter should not contain service markers");
    }

    [Fact]
    public void SplitFilter_OrWithMixedFields_MovesToService()
    {
        // Arrange: Create an OR expression with mixed regular and service fields
        Expression<Func<Person, bool>> filter = p => p.Name == "John" || ServiceExpressionMarker.MarkService(p.Id) > 21;

        // Act
        var result = splitter.SplitFilter(filter);

        // Assert - OR expressions with service fields should move entirely to service filter
        // because you can't safely split OR expressions
        Assert.Null(result.NonServiceFilter);
        Assert.NotNull(result.ServiceFilter);
    }

    [Fact]
    public void SplitFilter_ComplexNestedMixedFields_HandlesCorrectly()
    {
        // Arrange: Create a complex nested expression with mixed field types
        // (regularField1 AND regularField2) AND (serviceField1 OR serviceField2)
        Expression<Func<Person, bool>> filter = p => (p.Id > 1 && p.Name == "John") && (ServiceExpressionMarker.MarkService(p.Id) > 21 || ServiceExpressionMarker.MarkService(p.Name.Length) < 30);

        // Act
        var result = splitter.SplitFilter(filter);

        // Assert - should have both parts since they're connected by AND
        Assert.NotNull(result.NonServiceFilter);
        Assert.NotNull(result.ServiceFilter);

        // Assert that NonServiceFilter contains only non-service fields
        var nonServiceMarkerCheck = new ServiceMarkerCheckVisitor();
        nonServiceMarkerCheck.Visit(result.NonServiceFilter.Body);
        Assert.False(nonServiceMarkerCheck.ContainsServiceMarker, "NonServiceFilter should not contain service markers");
    }

    [Fact]
    public void SplitFilter_NotExpressionWithServiceField_MovesToService()
    {
        // Arrange: Create a NOT expression with a service field
        Expression<Func<Person, bool>> filter = p => !(ServiceExpressionMarker.MarkService(p.Id) > 21) && p.Id > 1;

        // Act
        var result = splitter.SplitFilter(filter);

        // Assert - should have both parts
        Assert.NotNull(result.NonServiceFilter);
        Assert.NotNull(result.ServiceFilter);

        // Assert that NonServiceFilter contains only non-service fields
        var nonServiceMarkerCheck = new ServiceMarkerCheckVisitor();
        nonServiceMarkerCheck.Visit(result.NonServiceFilter.Body);
        Assert.False(nonServiceMarkerCheck.ContainsServiceMarker, "NonServiceFilter should not contain service markers");
    }

    [Fact]
    public void FilterSplitResult_Properties_WorkCorrectly()
    {
        // Test the FilterSplitResult class itself
        var param = Expression.Parameter(typeof(Person), "p");
        var testExpr = Expression.Lambda<Func<Person, bool>>(Expression.Equal(Expression.Property(param, "Name"), Expression.Constant("test")), param);

        // Test constructor and properties
        var result1 = new FilterSplitResult(testExpr, null);
        Assert.Equal(testExpr, result1.NonServiceFilter);
        Assert.Null(result1.ServiceFilter);

        var result2 = new FilterSplitResult(null, testExpr);
        Assert.Null(result2.NonServiceFilter);
        Assert.Equal(testExpr, result2.ServiceFilter);

        var result3 = new FilterSplitResult(testExpr, testExpr);
        Assert.Equal(testExpr, result3.NonServiceFilter);
        Assert.Equal(testExpr, result3.ServiceFilter);
    }
}
