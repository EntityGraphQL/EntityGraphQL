# Optimizing Expressions

- [Linq.Expression.Optimizer](https://thorium.github.io/Linq.Expression.Optimizer/)
- [Linq Optimizer](https://github.com/nessos/LinqOptimizer)

EQL has a global BeforeExecuting hook on `ExecutionOptions` allowing you to intercept expressions before they are executed and modify them. **Linq.Expression.Optimizer** contains an expression tree visitor allowing you to simply:

```cs
  var options = new EntityGraphQL.Schema.ExecutionOptions();
  options.BeforeExecuting += (expression, isFinal) =>
  {
      // isFinal == true if the expression is the final execution - this means
      //  - ExecuteServiceFieldsSeparately = false, or
      //  - The query does not reference any fields with services
      //  - The query references fields with service and the first execution has completed (isFinal == false) and we are executing again to merge the service results
      return ExpressionOptimizer.visit(expression);
  };
```

**Linq Optimizer** appears to use expression methods on `IEnumerable<T>` which isn't directly compatible with this approach (there are expression trees in the project so it may work if you can use a lower level api call), you would need to test that the return type of expression is compatible.
