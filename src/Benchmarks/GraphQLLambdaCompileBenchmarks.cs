using System;
using System.Linq;
using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using EntityGraphQL.Compiler;
using EntityGraphQL.Compiler.Util;
using EntityGraphQL.Schema;

namespace Benchmarks;

[MemoryDiagnoser]
public class GraphQLLambdaCompileBenchmarks : BaseBenchmark
{
    private readonly LambdaExpression lambda;

    public GraphQLLambdaCompileBenchmarks()
    {
        const string query =
            @"{
            movie(id: ""077b3041-307a-42ba-9ffe-1121fcfc918b"") {
                id name released
                director {
                    id name dob
                    directorOf {
                        id name released
                    }
                }
                actors {
                    id name dob
                }
            }
        }";

        var document = GraphQLParser.Parse(query, Schema);
        var operation = document.Operations.Single();
        var compileContext = new CompileContext(new ExecutionOptions(), null, new QueryRequestContext(Schema.AuthorizationService, null), operation.OpVariableParameter, null);

        var fieldNode = operation.QueryFields.Single();
        var expandedField = fieldNode.Expand(compileContext, document.Fragments, false, operation.NextFieldContext!, operation.OpVariableParameter, null).Single();

        var contextParam = expandedField.RootParameter ?? throw new InvalidOperationException("Root parameter was not set");
        var replacer = new ParameterReplacer();
        var expression =
            expandedField.GetNodeExpression(compileContext, Services, document.Fragments, operation.OpVariableParameter, null, contextParam, false, null, null, false, replacer)
            ?? throw new InvalidOperationException("Failed to build field expression");

        var parameters = new[] { contextParam }.Concat(compileContext.ConstantParameters.Keys).ToArray();
        lambda = Expression.Lambda(expression, parameters);
    }

    [Benchmark]
    public Delegate Compile()
    {
        return lambda.Compile();
    }

    [Benchmark]
    public Delegate CompilePreferInterpretation()
    {
        return lambda.Compile(preferInterpretation: true);
    }
}
