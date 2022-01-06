---
title: "Introduction"
metaTitle: "EntityGraphQL introduction"
metaDescription: "Introduction to the EntityGraphql .NET GraphQL library"
---

# A GraphQL library for .NET

EntityGraphQL is a .NET library that allows you to easily build a GraphQL API on top of your data model with the extensibility to easily bring multiple data sources together in the single GraphQL schema.

Visit [graphql.org](https://graphql.org/learn/) to learn more about GraphQL.

EntityGraphQL builds a GraphQL schema that maps to .NET objects. It provides the functionality to parse a GraphQL query document and execute that against your mapped objects. These objects can be an Entity Framework `DbContext` or any other .NET object, it doesn't matter.

EntityGraphQL has been heavily tested against Entity Framework, although it does not require EF or any ORM.

A core feature of EntityGraphQL _with_ Entity Framework is that it builds selections of only the fields requested in the GraphQL query which means Entity Framework is not returning all columns from a table. This is done with the LING function `Select()` hence it works across any object tree.

Most people will want to use [EntityGraphQL.AspNet](https://www.nuget.org/packages/EntityGraphQL.AspNet) which integrates well with ASP.NET. However there is [EntityGraphQL](https://www.nuget.org/packages/EntityGraphQL) without the ASP.NET dependency if required.

**Please explore, give feedback and join the development.**

_If you're looking for a .NET library to generate code to query an C# API from a GraphQL schema see [DotNetGraphQLQueryGen](https://github.com/lukemurray/DotNetGraphQLQueryGen)_