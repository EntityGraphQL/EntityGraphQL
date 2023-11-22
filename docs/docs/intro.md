---
sidebar_position: 1
---

# Introduction

EntityGraphQL is a .NET library that allows you to easily build a [GraphQL API](https://graphql.org/learn/) on top of your data model with the extensibility to easily bring multiple data sources together in the single GraphQL schema.

EntityGraphQL builds a GraphQL schema that maps to .NET objects. It provides the functionality to parse a GraphQL query document and execute that against your mapped objects. These objects can be an Entity Framework `DbContext` or any other .NET object, it doesn't matter.

A core feature of EntityGraphQL _with_ Entity Framework (although EF is not a requirement) is that it builds selections of only the fields requested in the GraphQL query which means Entity Framework is not returning all columns from a table. This is done with the [LINQ](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/) projection operator [`Select()`](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/linq/projection-operations#select) hence it works across any object tree.

[EntityGraphQL.AspNet](https://www.nuget.org/packages/EntityGraphQL.AspNet) will get you easily set up with ASP.NET. However the core [EntityGraphQL](https://www.nuget.org/packages/EntityGraphQL) package has no ASP.NET dependency.

**Please explore, give feedback and join the development.**
