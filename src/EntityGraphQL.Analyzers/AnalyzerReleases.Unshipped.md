; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------
EGQL001 | EntityGraphQL.Performance | Info | Service field on an entity type has no bulk resolver
EGQL002 | EntityGraphQL.Correctness | Warning | Async field uses a service that is not thread-safe
EGQL003 | EntityGraphQL.Correctness | Warning | Field extension requires a collection field
EGQL004 | EntityGraphQL.Correctness | Warning | Use ResolveAsync for an expression that returns a Task
EGQL005 | EntityGraphQL.Correctness | Warning | Name is not a valid GraphQL name
EGQL006 | EntityGraphQL.Correctness | Warning | Subscription method must return IObservable<T>
EGQL007 | EntityGraphQL.Correctness | Warning | OneOf input type fields must all be nullable
EGQL008 | EntityGraphQL.Correctness | Warning | Field is added to the same type twice
EGQL009 | EntityGraphQL.Performance | Info | Use the async execute method inside an async method
EGQL010 | EntityGraphQL.Performance | Info | Resolver blocks on a Task instead of resolving asynchronously
