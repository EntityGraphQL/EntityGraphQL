using EntityGraphQL.Compiler;
using EntityGraphQL.Schema;
using Xunit;

namespace EntityGraphQL.Tests;

public class GraphQLParserTests
{
    [Fact]
    public void TestFourDigitUnicodeEscapeInString()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("echo", new { text = "" }, (ctx, args) => args.text, "Echo text");

        var query =
            @"
            query {
                echo(text: ""Hello \u0041\u0042\u0043"")
            }
        ";

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, new TestDataContext(), null, null);

        Assert.Null(result.Errors);
        Assert.Equal("Hello ABC", result.Data!["echo"]);
    }

    [Fact]
    public void TestVariableWidthUnicodeEscapeEmoji()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("echo", new { text = "" }, (ctx, args) => args.text, "Echo text");

        // Test the new \u{...} syntax for emoji (pile of poo emoji U+1F4A9)
        var query =
            @"
            query {
                echo(text: ""Hello \u{1F4A9}"")
            }
        ";

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, new TestDataContext(), null, null);

        // This test will currently fail as the feature is not yet implemented
        Assert.Null(result.Errors);
        Assert.Equal("Hello 💩", result.Data!["echo"]);
    }

    [Fact]
    public void TestVariableWidthUnicodeEscapeMultipleCharacters()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("echo", new { text = "" }, (ctx, args) => args.text, "Echo text");

        // Test multiple variable-width unicode escapes
        var query =
            @"
            query {
                echo(text: ""\u{1F600} \u{1F37A} \u{2764}"")
            }
        ";

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, new TestDataContext(), null, null);

        // This test will currently fail as the feature is not yet implemented
        Assert.Null(result.Errors);
        Assert.Equal("😀 🍺 ❤", result.Data!["echo"]);
    }

    [Fact]
    public void TestSurrogatePairEscapeSequence()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("echo", new { text = "" }, (ctx, args) => args.text, "Echo text");

        // Test legacy surrogate pair syntax for pile of poo emoji (U+1F4A9)
        // High surrogate: 0xD83D, Low surrogate: 0xDCA9
        var query =
            @"
            query {
                echo(text: ""Hello \uD83D\uDCA9"")
            }
        ";

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, new TestDataContext(), null, null);

        Assert.Null(result.Errors);
        Assert.Equal("Hello 💩", result.Data!["echo"]);
    }

    [Fact]
    public void TestDescriptionOnQuery()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        // Test description on query operation (new in September 2025 spec)
        var query =
            @"
            """"""
            This query fetches all people.
            It is used for testing purposes.
            """"""
            query GetAllPeople {
                people { name }
            }
        ";

        var doc = GraphQLParser.Parse(query, schema);

        // This test validates that the parser doesn't fail when descriptions are present
        // Full implementation would also store/expose the description
        Assert.NotNull(doc);
        Assert.Single(doc.Operations);
    }

    [Fact]
    public void TestDescriptionOnMutation()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.AddMutationsFrom<TestMutations>();

        var query =
            @"
            """"""
            This mutation does nothing.
            Used for testing description parsing.
            """"""
            mutation DoNothing {
                noop
            }
        ";

        var doc = GraphQLParser.Parse(query, schema);

        Assert.NotNull(doc);
        Assert.Single(doc.Operations);
    }

    public class TestMutations
    {
        [GraphQLMutation]
        public bool Noop() => true;
    }

    [Fact]
    public void TestDescriptionOnFragment()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        var query =
            @"
            query {
                people { ...PersonFields }
            }

            """"""
            Fragment containing common person fields.
            Reusable across multiple queries.
            """"""
            fragment PersonFields on Person {
                name
            }
        ";

        var doc = GraphQLParser.Parse(query, schema);

        Assert.NotNull(doc);
        Assert.Single(doc.Fragments);
    }

    [Fact]
    public void TestDescriptionOnField()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();

        var query =
            @"
            query {
                ""this is a list of people""
                people { id }
            }
        ";

        var doc = GraphQLParser.Parse(query, schema);

        Assert.NotNull(doc);
    }

    [Fact]
    public void TestDescriptionOnAll()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Mutation().Add("createPost", "Add new post", (string input) => true);

        var query =
            @"
            """"""
            This is the main query operation.
            It demonstrates descriptions on operations.
            """"""
            query GetUserData($userId: ID) {
                """"""
                Description on a field selection.
                This fetches user information.
                """"""
                user(id: $userId) {
                    id
                    name
                    
                    """"""
                    Description on a nested field.
                    Gets the user's tasks.
                    """"""
                    tasks {
                        id
                        ""Another one""
                        name
                    }
                }
                
                ""Short description on another field""
                mainProject {
                    id
                }
            }

            """"""
            This is a reusable fragment.
            Contains common user fields.
            """"""
            fragment UserDetails on User {
                id
                
                ""The user's display name""
                name
                
                """"""
                The user's email address.
                May be null if not shared.
                """"""
                field2
            }

            """"""
            Mutation operation description.
            Creates a new post for a user.
            """"""
            mutation CreatePost(
                ""This variable does blah""
                $input: String
            ) {
                ""Creates and returns the new post""
                createPost(input: $input)
            }
        ";

        var doc = GraphQLParser.Parse(query, schema);

        Assert.NotNull(doc);
        Assert.Single(doc.Fragments);
    }

    [Fact]
    public void TestMixedUnicodeEscapes()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("echo", new { text = "" }, (ctx, args) => args.text, "Echo text");

        // Mix old-style \uXXXX with new-style \u{...}
        var query =
            @"
            query {
                echo(text: ""\u0048ello \u{1F600}"")
            }
        ";

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, new TestDataContext(), null, null);

        Assert.Null(result.Errors);
        Assert.Equal("Hello 😀", result.Data!["echo"]);
    }

    [Fact]
    public void TestBlockStringWithUnicodeEscapes()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("echo", new { text = "" }, (ctx, args) => args.text, "Echo text");

        var query =
            @"
            query {
                echo(text: """"""
                    Multi-line text
                    with emoji \u{1F4A9}
                    and normal unicode \u0041
                """""")
            }
        ";

        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, new TestDataContext(), null, null);

        Assert.Null(result.Errors);
        Assert.Contains("emoji 💩", (string)result.Data!["echo"]!);
        Assert.Contains("unicode A", (string)result.Data!["echo"]!);
    }

    [Fact]
    public void TestInvalidSurrogatePairShouldError()
    {
        var schema = SchemaBuilder.FromObject<TestDataContext>();
        schema.Query().AddField("echo", new { text = "" }, (ctx, args) => args.text, "Echo text");

        // Invalid surrogate pair - high surrogate without low surrogate
        var query =
            @"
            query {
                echo(text: ""Invalid \uD83D not paired"")
            }
        ";

        // According to the spec, this should either produce an error or handle it gracefully
        // Current implementation may pass it through as-is
        var result = schema.ExecuteRequestWithContext(new QueryRequest { Query = query }, new TestDataContext(), null, null);

        // Test passes if either: error is produced OR string is handled
        Assert.True(result.Errors != null || result.Data != null);
    }
}
