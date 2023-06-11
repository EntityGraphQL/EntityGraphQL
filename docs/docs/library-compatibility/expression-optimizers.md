# Optimizing Expressions

* [Linq.Expression.Optimizer](https://thorium.github.io/Linq.Expression.Optimizer/)
* [Linq Optimizer](https://github.com/nessos/LinqOptimizer)

EQL has a global BeforeExecuting hook on `ExecutionOptions` allowing you to intercept expressions before they are executed and modify them.  **Linq.Expression.Optimizer** contains an expression tree visitor allowing you to simply:

```cs
  var options = new EntityGraphQL.Schema.ExecutionOptions();
  options.BeforeExecuting += (x) =>
  {
      return ExpressionOptimizer.visit(x);
  };
```

**Linq Optimizer** appears to use expression methods on `IEnumerable<T>` which isn't compatible with this approach (there are expression trees in the project so it may work if you can use a lower level api call).

