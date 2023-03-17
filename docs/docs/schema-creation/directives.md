---
sidebar_position: 9
---

# Directives

[Directives](https://graphql.org/learn/queries/#directives) provide a way to dynamically change the structure and shape of our queries using variables. An example from the GraphQL website:

```graphql
query Hero($episode: Episode, $withFriends: Boolean!) {
  hero(episode: $episode) {
    name
    friends @include(if: $withFriends) {
      name
    }
  }
}
```

Now if you set `withFriends` to `true` in the variables passed with the query POST you'll get the `friends` result. If you set it to `false` you will not. Thus dynamically changing the shape of your query.

## Built-In Directives

The GraphQL spec defines 2 directives that are supported out of the box in EntityGraphQL.

- `@include(if: Boolean)` - Only include this field in the result if the argument is true.
- `@skip(if: Boolean)` - Skip this field if the argument is true.

## Custom Directives

Directives give you access to the GraphQL Nodes - a abstract-code representation of the query document. You can return new `IGraphQLNode` with modified data to make changes before executing.

The main interface is `IGraphQLNode? VisitNode(ExecutableDirectiveLocation location, IGraphQLNode? node, object? arguments)` - This is called when the node in the document graph is seen. You can return `null` to remove the node from the result.
- `location` will tell you the location/type of the node the directive was added to (e.g. field, fragment spread)
-  `node` is information about the node.
- `arguments` are any arguments passed to the directive

_We're keen to hear if you can or cannot implement the functionality you require with the above interface. Please open an issue describing what you are trying to implement if you have issues. You may also want to look at [Field Extensions](../field-extensions/)._

### Example

Here is a simple directive that rewrites the expression to provide date formatting in the query.

```cs
public class FormatDirective : DirectiveProcessor<FormatDirectiveArgs>
{
  public override string Name => "format";
  public override string Description => "Formats DateTime scalar values";
  public override List<ExecutableDirectiveLocation> Location => new() { ExecutableDirectiveLocation.FIELD };

  public override IGraphQLNode VisitNode(ExecutableDirectiveLocation location, IGraphQLNode node, object arguments)
  {
    if (arguments is FormatDirectiveArgs args)
    {
      // only operate on scalar nodes
      if (node is GraphQLScalarField fieldNode)
      {
        var expression = fieldNode.NextFieldContext;
        if (expression.Type != typeof(DateTime) && expression.Type != typeof(DateTime?))
          throw new EntityGraphQLException("The format directive can only be used on DateTime fields");

        if (expression.Type == typeof(DateTime?))
          expression = Expression.Property(expression, "Value"); 
        expression = Expression.Call(expression, "ToString", null, Expression.Constant(args.As));
        return new GraphQLScalarField(fieldNode.Schema, fieldNode.Field, fieldNode.Name, expression, fieldNode.RootParameter, fieldNode.ParentNode, fieldNode.Arguments);
      }
    }
    return node;
  }
}

internal class FormatDirectiveArgs
{
  [GraphQLField("as", "The format to use")]
  public string As { get; set; }
}

// Add it to you schema
schema.AddDirective(new FormatDirective());
```

Use it like this...

```gql
query {
  people {
    id
    birthday @format(as: "dd MMM yyyy")
  }
}
```
