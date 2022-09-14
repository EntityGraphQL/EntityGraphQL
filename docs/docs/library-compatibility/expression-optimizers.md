# Optimizing Expressions

* [Linq.Expression.Optimizer/](https://thorium.github.io/Linq.Expression.Optimizer/)
* [Linq Optimizer](https://github.com/nessos/LinqOptimizer)

Currently there's no global hook in EQL to attach a linq optimizer, you may be able to override individual resolve statements but it is unlikely to be able to optimize as well due to conditionals like WhenWhere and arguments not yet being supplied.

```
type.AddField(
    "people",
    (context) => ExpressionOptimizer.visit(context.people.Where(x => x.Name == "Frank")),
    "people"
  );
```