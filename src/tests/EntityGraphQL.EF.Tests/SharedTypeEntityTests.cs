using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EntityGraphQL.EF.Tests;

/// <summary>
/// EF Core shared-type entities - tables mapped to property bags (Dictionary&lt;string, object&gt;) with no
/// class per table, as when the tables are only known at runtime. Every table is the same dotnet type,
/// so each field names its schema type with Returns("Type"), and selections, filters, sorts and
/// relations have to come from the field, then translate to SQL (SQLite throws if they do not).
/// </summary>
public class SharedTypeEntityTests
{
    private sealed class BagContext(DbContextOptions<BagContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder model)
        {
            model.SharedTypeEntity<Dictionary<string, object>>(
                "authors",
                b =>
                {
                    b.IndexerProperty<int>("id");
                    b.IndexerProperty<string>("name");
                    b.HasKey("id");
                }
            );
            model.SharedTypeEntity<Dictionary<string, object>>(
                "priorities",
                b =>
                {
                    b.IndexerProperty<int>("id");
                    b.IndexerProperty<string>("mergeRequest");
                    b.IndexerProperty<int>("score");
                    b.IndexerProperty<int>("authorId");
                    b.HasKey("id");
                    b.HasOne("authors", "author").WithMany("priorities").HasForeignKey("authorId");
                }
            );
        }
    }

    private static (SchemaProvider<BagContext> Schema, BagContext Db, SqliteConnection Connection) Build(bool paged = false)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var db = new BagContext(new DbContextOptionsBuilder<BagContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        db.Set<Dictionary<string, object>>("authors").AddRange(new Dictionary<string, object> { ["id"] = 1, ["name"] = "Ada" }, new Dictionary<string, object> { ["id"] = 2, ["name"] = "Ben" });
        db.Set<Dictionary<string, object>>("priorities")
            .AddRange(
                new Dictionary<string, object>
                {
                    ["id"] = 1,
                    ["mergeRequest"] = "!12",
                    ["score"] = 9,
                    ["authorId"] = 1,
                },
                new Dictionary<string, object>
                {
                    ["id"] = 2,
                    ["mergeRequest"] = "!13",
                    ["score"] = 3,
                    ["authorId"] = 2,
                },
                new Dictionary<string, object>
                {
                    ["id"] = 3,
                    ["mergeRequest"] = "!14",
                    ["score"] = 7,
                    ["authorId"] = 1,
                }
            );
        db.SaveChanges();
        db.ChangeTracker.Clear();

        var schema = new SchemaProvider<BagContext>();
        var author = schema.AddType<Dictionary<string, object>>("Author", null);
        var priority = schema.AddType<Dictionary<string, object>>("Priority", null);

        author.AddField("id", a => Microsoft.EntityFrameworkCore.EF.Property<int>(a, "id"), null);
        author.AddField("name", a => Microsoft.EntityFrameworkCore.EF.Property<string>(a, "name"), null);
        author.AddField("priorities", a => Microsoft.EntityFrameworkCore.EF.Property<IEnumerable<Dictionary<string, object>>>(a, "priorities"), null).Returns("Priority");

        priority.AddField("id", p => Microsoft.EntityFrameworkCore.EF.Property<int>(p, "id"), null);
        priority.AddField("mergeRequest", p => Microsoft.EntityFrameworkCore.EF.Property<string>(p, "mergeRequest"), null);
        priority.AddField("score", p => Microsoft.EntityFrameworkCore.EF.Property<int>(p, "score"), null);
        priority.AddField("author", p => Microsoft.EntityFrameworkCore.EF.Property<Dictionary<string, object>>(p, "author"), null).Returns("Author");

        var authors = schema.Query().AddField("authors", c => c.Set<Dictionary<string, object>>("authors"), null).Returns("Author").UseFilter().UseSort(true);
        var priorities = schema.Query().AddField("priorities", c => c.Set<Dictionary<string, object>>("priorities"), null).Returns("Priority").UseFilter().UseSort(true);
        if (paged)
        {
            authors.UseConnectionPaging();
            priorities.UseOffsetPaging();
        }
        return (schema, db, connection);
    }

    private static List<dynamic> Run(string query, string field, bool paged = false)
    {
        var (schema, db, connection) = Build(paged);
        using (connection)
        using (db)
        {
            var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, db, null, null);
            Assert.Null(result.Errors);
            var value = ((IDictionary<string, object?>)result.Data!)[field]!;
            return value is IEnumerable<dynamic> list ? Enumerable.ToList(list) : [value];
        }
    }

    [Fact]
    public void FilterTranslates()
    {
        var priorities = Run("{ priorities(filter: \"score > 5\") { mergeRequest } }", "priorities");
        Assert.Equal(["!12", "!14"], priorities.Select(p => (string)p.mergeRequest));
    }

    [Fact]
    public void FilterThroughAReferenceTranslates()
    {
        var priorities = Run("{ priorities(filter: \"author.name == \\\"Ben\\\"\") { mergeRequest } }", "priorities");
        Assert.Equal(["!13"], priorities.Select(p => (string)p.mergeRequest));
    }

    [Fact]
    public void FilterMethodOnACollectionTranslates()
    {
        var authors = Run("{ authors(filter: \"priorities.any(score > 8)\") { name } }", "authors");
        Assert.Equal(["Ada"], authors.Select(a => (string)a.name));
    }

    [Fact]
    public void SortTranslates()
    {
        var priorities = Run("{ priorities(sort: [{ score: DESC }]) { mergeRequest } }", "priorities");
        Assert.Equal(["!12", "!14", "!13"], priorities.Select(p => (string)p.mergeRequest));
    }

    [Fact]
    public void ManyToOneTranslates()
    {
        var priorities = Run("{ priorities(sort: [{ id: ASC }]) { mergeRequest author { name } } }", "priorities");
        Assert.Equal(["Ada", "Ben", "Ada"], priorities.Select(p => (string)p.author.name));
    }

    [Fact]
    public void OneToManyTranslates()
    {
        var authors = Run("{ authors(sort: [{ id: ASC }]) { name priorities { mergeRequest } } }", "authors");
        Assert.Equal(2, Enumerable.Count(authors[0].priorities));
        Assert.Equal(1, Enumerable.Count(authors[1].priorities));
    }

    [Fact]
    public void FilterAndOffsetPagingTranslate()
    {
        var page = Run("{ priorities(filter: \"score > 5\", sort: [{ score: ASC }], take: 1) { items { mergeRequest } totalItems } }", "priorities", paged: true);
        Assert.Equal(2, (int)page[0].totalItems);
        Assert.Equal("!14", (string)Enumerable.First(page[0].items).mergeRequest);
    }

    [Fact]
    public void FilterAndConnectionPagingTranslate()
    {
        var connection = Run("{ authors(filter: \"priorities.any(score > 8)\", first: 5) { totalCount edges { node { name } } } }", "authors", paged: true);
        Assert.Equal(1, (int)connection[0].totalCount);
        Assert.Equal("Ada", (string)Enumerable.First(connection[0].edges).node.name);
    }
}
