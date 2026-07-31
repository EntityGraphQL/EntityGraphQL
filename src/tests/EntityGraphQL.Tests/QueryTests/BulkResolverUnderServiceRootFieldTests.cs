using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static EntityGraphQL.Schema.ArgumentHelper;

namespace EntityGraphQL.Tests;

/// <summary>
/// A ResolveBulk field selected under a root field that is itself resolved from services now participates in
/// the two-pass flow and bulk-loads once. The same bulk field selected at two levels of a self-referencing
/// type loads with every level's keys in one call. Both used to fail with an internal exception reaching the
/// client as "Field '&lt;root field&gt;' - Error occurred" (or fall back to per-item Resolve).
/// </summary>
public class BulkResolverUnderServiceRootFieldTests
{
    private class Note
    {
        public Guid Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
        public List<Note> Children { get; set; } = [];
        public List<Tag> Tags { get; set; } = [];
    }

    private class Tag
    {
        public Guid Id { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    private class NotesContext
    {
        public List<Note> Notes { get; set; } = [];
    }

    private class FlagService
    {
        public int ItemCalls { get; private set; }
        public int BulkCalls { get; private set; }
        public int BulkKeys { get; private set; }

        public bool IsFlagged(Guid id)
        {
            ItemCalls++;
            return true;
        }

        public IDictionary<Guid, bool> GetFlags(IEnumerable<Guid> ids)
        {
            BulkCalls++;
            var keys = ids.ToList();
            BulkKeys += keys.Count;
            return keys.Distinct().ToDictionary(i => i, _ => true);
        }
    }

    /// <summary>
    /// isFlagged is a per-item service field with a bulk resolver - the shape of an
    /// "isReadByCurrentUser"-style field.
    /// </summary>
    private static SchemaProvider<NotesContext> BuildSchema()
    {
        var schema = SchemaBuilder.FromObject<NotesContext>();
        schema
            .Type<Note>()
            .AddField("isFlagged", "Read state for the current user")
            .Resolve<FlagService>((note, srv) => srv.IsFlagged(note.Id))
            .ResolveBulk<FlagService, Guid, bool>(note => note.Id, (ids, srv) => srv.GetFlags(ids));
        return schema;
    }

    private static QueryResult Execute(SchemaProvider<NotesContext> schema, FlagService flagService, QueryRequest gql, ExecutionOptions? options = null)
    {
        var parentId = Guid.NewGuid();
        var context = new NotesContext
        {
            Notes =
            [
                new Note
                {
                    Id = parentId,
                    Text = "parent",
                    Tags = [new Tag { Id = Guid.NewGuid(), Label = "tag-1" }],
                    Children =
                    [
                        new Note
                        {
                            Id = Guid.NewGuid(),
                            Text = "child",
                            ParentId = parentId,
                            Tags = [new Tag { Id = Guid.NewGuid(), Label = "tag-2" }],
                        },
                    ],
                },
            ],
        };
        var services = new ServiceCollection();
        services.AddSingleton(flagService);
        // the reported schema resolves its root field with the query context pulled from DI
        services.AddSingleton(context);
        return schema.ExecuteRequestWithContext(gql, context, services.BuildServiceProvider(), null, options);
    }

    /// <summary>
    /// The bulk field is selected on a list that comes from a service, not from the context. Root service
    /// lists participate in the two-pass flow so the bulk loader runs once.
    /// </summary>
    [Fact]
    public void BulkFieldUnderServiceResolvedRootField()
    {
        var schema = BuildSchema();
        schema.Query().AddField("rootNotes", "Notes from a service").Resolve<NotesContext>((_, db) => db.Notes.ToList());
        var flagService = new FlagService();

        var res = Execute(schema, flagService, new QueryRequest { Query = "{ rootNotes { id isFlagged } }" });

        Assert.Null(res.Errors);
        Assert.True(((dynamic)res.Data!["rootNotes"]!)[0].isFlagged);
        Assert.Equal(1, flagService.BulkCalls);
        Assert.Equal(0, flagService.ItemCalls);
    }

    /// <summary>
    /// Same as above with services in a single pass - no separate first pass to collect keys, so the
    /// per-item resolver runs instead.
    /// </summary>
    [Fact]
    public void BulkFieldUnderServiceResolvedRootField_SinglePass()
    {
        var schema = BuildSchema();
        schema.Query().AddField("rootNotes", "Notes from a service").Resolve<NotesContext>((_, db) => db.Notes.ToList());
        var flagService = new FlagService();

        var res = Execute(schema, flagService, new QueryRequest { Query = "{ rootNotes { id isFlagged } }" }, new ExecutionOptions { ExecuteServiceFieldsSeparately = false });

        Assert.Null(res.Errors);
        Assert.True(((dynamic)res.Data!["rootNotes"]!)[0].isFlagged);
        Assert.Equal(0, flagService.BulkCalls);
        Assert.Equal(1, flagService.ItemCalls);
    }

    /// <summary>
    /// Same field selected at two levels of a self-referencing type. Both levels are loaded in a single bulk
    /// call - the loaded data is keyed by the bulk resolver name, so loading a level at a time had the last
    /// level's data replace the previous level's and the lookup threw for keys that were never loaded.
    /// </summary>
    [Fact]
    public void BulkFieldAtTwoLevelsOfSelfReferencingType()
    {
        var schema = BuildSchema();
        var flagService = new FlagService();

        var res = Execute(schema, flagService, new QueryRequest { Query = "{ notes { id isFlagged children { id isFlagged } } }" });

        Assert.Null(res.Errors);
        dynamic notes = res.Data!["notes"]!;
        Assert.True(notes[0].isFlagged);
        Assert.True(notes[0].children[0].isFlagged);
        Assert.Equal(1, flagService.BulkCalls);
        Assert.Equal(2, flagService.BulkKeys); // the parent's and the child's key in the one call
        Assert.Equal(0, flagService.ItemCalls);
    }

    /// <summary>
    /// The reported query shape: root field with args resolved from two services (one being the query
    /// context), a self-referencing child list selecting the same fields as its parent, a nested list of
    /// value objects at both levels and __typename throughout.
    /// </summary>
    [Fact]
    public void ReportedShape()
    {
        var schema = BuildSchema();
        schema
            .Query()
            .AddField("rootNotes", new { kind = Required<string>(), ownerId = Required<Guid>() }, "Top level notes for an owner")
            .Resolve<FlagService, NotesContext>((_, args, srv, db) => db.Notes.Where(n => n.ParentId == null && n.Text == args.kind).ToList());
        var flagService = new FlagService();

        var gql = new QueryRequest
        {
            Query =
                @"query Notes($kind: String!, $ownerId: ID!) {
                    rootNotes(kind: $kind, ownerId: $ownerId) {
                        id
                        text
                        parentId
                        isFlagged
                        tags { id label __typename }
                        children {
                            id
                            text
                            parentId
                            isFlagged
                            tags { id label __typename }
                            __typename
                        }
                        __typename
                    }
                }",
            Variables = new QueryVariables { { "kind", "parent" }, { "ownerId", Guid.NewGuid().ToString() } },
        };

        var res = Execute(schema, flagService, gql);

        Assert.Null(res.Errors);
        dynamic notes = res.Data!["rootNotes"]!;
        Assert.Single(notes);
        Assert.True(notes[0].isFlagged);
        Assert.True(notes[0].children[0].isFlagged);
        // root service list participates in two-pass - one bulk load with parent + child keys
        Assert.Equal(1, flagService.BulkCalls);
        Assert.Equal(2, flagService.BulkKeys);
        Assert.Equal(0, flagService.ItemCalls);
    }
}
