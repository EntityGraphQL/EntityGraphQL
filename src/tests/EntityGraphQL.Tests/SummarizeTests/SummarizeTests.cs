using Xunit;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using EntityGraphQL.Schema.FieldExtensions;

namespace EntityGraphQL.Tests
{
    public class SummarizeTests
    {
        [Fact]
        public void SupportUseSummarize()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(false);
            schema.Type<TestDataContext>().GetField("people", null)
                .UseSummarize();

            Assert.True(schema.HasType("PersonSummary"));

            var type = schema.GetSchemaType("PersonSummary", null);
            Assert.True(type.HasField("sum", null));
            Assert.True(type.HasField("max", null));
            Assert.True(type.HasField("min", null));
            Assert.True(type.HasField("avg", null));
            Assert.True(type.HasField("count", null));
        }

        [Fact]
        public void SupportUseSummaryCount()
        {
            var schema = SchemaBuilder.FromObject<TestDataContext>(false);
            schema.Type<TestDataContext>().GetField("people", null)
               .UseSummarize();

            var gql = new QueryRequest
            {
                Query = @"query() {
                    people() { 
                        summarize { count }
                    }
                }",
                Variables = new QueryVariables{}
            };
            var context = new TestDataContext().FillWithTestData();
            context.People.Add(new Person
            {
                LastName = "Zoo",
                Height = 1
            });


            var tree = schema.ExecuteRequest(gql, context, null, null);
            Assert.Null(tree.Errors);
            dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
            Assert.Equal(2, Enumerable.Count(people));
            var person = Enumerable.First(people);
            Assert.Equal(2, person.summarize.count);            
        }

        //[Fact]
        //public void SupportUseSummaryMax()
        //{
        //    var schema = SchemaBuilder.FromObject<TestDataContext>(false);
        //    schema.Type<TestDataContext>().GetField("people", null)
        //       .UseSummarize();

        //    var gql = new QueryRequest
        //    {
        //        Query = @"query() {
        //            people() { 
        //                summarize { max { height } }
        //            }
        //        }",
        //        Variables = new QueryVariables { }
        //    };
        //    var context = new TestDataContext().FillWithTestData();
        //    context.People.Add(new Person
        //    {
        //        LastName = "Zoo",
        //        Height = 1
        //    });


        //    var tree = schema.ExecuteRequest(gql, context, null, null);
        //    Assert.Null(tree.Errors);
        //    dynamic people = ((IDictionary<string, object>)tree.Data)["people"];
        //    Assert.Equal(2, Enumerable.Count(people));
        //    var person = Enumerable.First(people);
        //    Assert.Equal(2, person.summarize.max.height);
        //}

    }
}