using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using System.Linq.Expressions;
using System;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace EntityGraphQL.Tests
{
    public class PeopleMutations : IMutations
    {
        private static int idCount = 0;
        [GraphQLMutation]

        public Person AddPerson(PeopleMutationsArgs args)
        {
            return new Person { Name = string.IsNullOrEmpty(args.Name) ? "Default" : args.Name, Id = 555, Projects = new List<Project>() };
        }


        [GraphQLMutation]

        public int DefaultValueTest(int valueWithDefault = 8)
        {
            return valueWithDefault;
        }

        [GraphQLMutation]

        public Person AddPersonSeparateArguments(string name, List<string> names, InputObject nameInput, Gender? gender)
        {
            return new Person { Name = string.IsNullOrEmpty(name) ? "Default" : name, Id = 555, Projects = new List<Project>() };
        }

        [GraphQLMutation]

        public Person AddPersonPrimitive(int id, string name, DateTime birthday, decimal weight, Gender? gender)
        {
            return new Person { Name = string.IsNullOrEmpty(name) ? "Default" : name, Id = 555, Projects = new List<Project>() };
        }

        [GraphQLMutation]

        public Person AddPersonSingleArgument([GraphQLInputType] InputObject nameInput)
        {
            return new Person { Name = string.IsNullOrEmpty(nameInput.Name) ? "Default" : nameInput.Name, Id = 555, Projects = new List<Project>() };
        }

        [GraphQLMutation]

        public Person AddInputWithChildWithId(ListOfObjectsWithIds nameInput)
        {
            return null;
        }

#nullable enable
        [GraphQLMutation]

        public Person AddPersonNullableNestedType([GraphQLInputType] NestedInputObject required, [GraphQLInputType] NestedInputObject? optional)
        {
            return new Person { Name = string.IsNullOrEmpty(required.Name) ? "Default" : required.Name, Id = 555, Projects = new List<Project>() };
        }
#nullable restore

        [GraphQLMutation]

        public Person AddPersonSingleArgumentNestedType(NestedInputObject nameInput)
        {
            return new Person { Name = string.IsNullOrEmpty(nameInput.Name) ? "Default" : nameInput.Name, Id = 555, Projects = new List<Project>() };
        }

        [GraphQLMutation]

        public Expression<Func<TestDataContext, Person>> AddPersonNames(TestDataContext db, PeopleMutationsArgs args)
        {
            var id = 11;
            var newPerson = new Person { Id = id, Name = args.Names[0], LastName = args.Names[1] };
            db.People.Add(newPerson);
            return ctx => ctx.People.First(p => p.Id == id);
        }
        [GraphQLMutation]

        public async Task<Expression<Func<TestDataContext, Person>>> AddPersonNamesAsync(TestDataContext db, PeopleMutationsArgs args)
        {
            var id = 11;
            var newPerson = new Person { Id = id, Name = args.Names[0], LastName = args.Names[1] };
            db.People.Add(newPerson);
            await System.Threading.Tasks.Task.Delay(1);
            return ctx => ctx.People.First(p => p.Id == id);
        }
        [GraphQLMutation]

        public Expression<Func<TestDataContext, Person>> AddPersonNamesExpression(TestDataContext db, PeopleMutationsArgs args)
        {
            var newPerson = new Person { Id = idCount++, Name = args.Names[0], LastName = args.Names[1] };
            db.People.Add(newPerson);
            return ctx => ctx.People.First(p => p.Id == newPerson.Id);
        }

        [GraphQLMutation]

        public Person AddPersonInput(PeopleMutationsArgs args)
        {
            return new Person { Name = args.NameInput.Name, LastName = args.NameInput.LastName };
        }

        [GraphQLMutation]

        public float AddFloat(FloatInput args)
        {
            return args.Float;
        }
        [GraphQLMutation]

        public double AddDouble(DoubleInput args)
        {
            return args.Double;
        }
        [GraphQLMutation]

        public decimal AddDecimal(DecimalInput args)
        {
            return args.Decimal;
        }

        [GraphQLMutation]
        public Expression<Func<TestDataContext, Person>> AddPersonAdv(PeopleMutationsArgs args)
        {
            // test returning a constant in the expression which allows GraphQL selection over the schema (assuming the constant is a type in the schema)
            // Ie. in the mutation query you can select any valid fields in the schema from Person
            var person = new Person
            {
                Name = args.Name,
                Tasks = new List<Task> { new Task { Name = "A" } },
                Projects = new List<Project> { new Project { Id = 123 } }
            };
            return ctx => person;
        }
        [GraphQLMutation]
        public Expression<Func<TestDataContext, IEnumerable<Person>>> AddPersonAdvList(PeopleMutationsArgs args)
        {
            // test returning a constant in the expression which allows GraphQL selection over the schema (assuming the constant is a type in the schema)
            // Ie. in the mutation query you can select any valid fields in the schema from Person
            var person = new Person
            {
                Name = args.Name,
                Tasks = new List<Task> { new Task { Name = "A" } },
                Projects = new List<Project> { new Project { Id = 123 } }
            };
            return ctx => new List<Person> { person };
        }

        [GraphQLMutation]
        public Expression<Func<TestDataContext, IEnumerable<Person>>> AddPersonReturnAll(TestDataContext db, PeopleMutationsArgs args)
        {
            db.People.Add(new Person { Id = 11, Name = args.Name });
            return ctx => ctx.People;
        }

        [GraphQLMutation]
        public IEnumerable<Person> AddPersonReturnAllConst(TestDataContext db, PeopleMutationsArgs args)
        {
            db.People.Add(new Person { Id = 11, Name = args.Name });
            return db.People.ToList();
        }

        [GraphQLMutation]
        public int AddPersonError(PeopleMutationsArgs args)
        {
            throw new EntityGraphQLArgumentException("name", "Name can not be null");
        }

        [GraphQLMutation]
        public int AddPersonErrorUnexposedException(PeopleMutationsArgs args)
        {
            throw new Exception("You should not see this message outside of Development");
        }

        [GraphQLMutation]
        public async Task<bool> DoGreatThing()
        {
            return await Task<bool>.Run(() =>
            {
                return true;
            });
        }
        [GraphQLMutation]
        public Task<bool> DoGreatThingWithoutAsyncKeyword() => DoGreatThing();
        [GraphQLMutation]
        public static async Task<bool> DoGreatThingStaticly()
        {
            return await Task<bool>.Run(() =>
            {
                return true;
            });
        }
        [GraphQLMutation]
        public async Task<bool> NeedsGuid(GuidArgs args)
        {
            return await Task<bool>.Run(() =>
            {
                return true;
            });
        }
        [GraphQLMutation]
        public async Task<bool> NeedsGuidNonNull(GuidNonNullArgs args)
        {
            return await Task<bool>.Run(() =>
            {
                return true;
            });
        }
        [GraphQLMutation]
        public bool TaskWithList(ListArgs args)
        {
            if (args?.Inputs == null)
                throw new Exception("Inputs can not be null");
            return true;
        }
        [GraphQLMutation]
        public bool TaskWithListSeparateArg(List<InputObject> inputs) => true;

        [GraphQLMutation]
        public bool TaskWithListInt(ListIntArgs args)
        {
            return true;
        }

        [GraphQLMutation]
        public async Task<bool> regexValidation(RegexArgs args)
        {
            return await Task<bool>.Run(() =>
            {
                return true;
            });
        }

        [GraphQLMutation]
        static public bool NullableGuidArgs(NullableGuidArgs args)
        {
            if (args.Id != null)
                throw new ArgumentException("Guid can not be a value");
            if (args.Int != null)
                throw new ArgumentException("Int can not be a value");
            if (args.Float != null)
                throw new ArgumentException("Float can not be a value");
            if (args.Double != null)
                throw new ArgumentException("Double can not be a value");
            if (args.Bool != null)
                throw new ArgumentException("Bool can not be a value");
            if (args.Enum != null)
                throw new ArgumentException("Enum can not be a value");
            return true;
        }
        [GraphQLMutation]
        static public string[] ListOfGuidArgs(ListOfGuidArgs args)
        {
            if (args.Ids == null)
                throw new ArgumentException("Ids can not be null");
            if (args.Ids.Any(i => i == Guid.Empty))
                throw new ArgumentException("Ids can not be empty GUID values");
            return args.Ids.Select(g => g.ToString()).ToArray();
        }
    }

    public class NestedInputObject
    {
        public string Name { get; set; }
        public string LastName { get; set; }
        public DateTime? Birthday { get; set; }
    }

    [GraphQLArguments]
    public class ListArgs
    {
        public List<InputObject> Inputs { get; set; }
    }

    [GraphQLArguments]
    public class ListIntArgs
    {
        public List<InputObjectId> Inputs { get; set; }
    }

    [GraphQLArguments]
    public class PeopleMutationsArgs
    {
        public string Name { get; set; }
        public List<string> Names { get; set; }

        public InputObject NameInput { get; set; }
        public Gender? Gender { get; set; }
    }
    [GraphQLArguments]
    public class NullableGuidArgs
    {
        public Guid? Id { get; set; }
        public int? Int { get; set; }
        public float? Float { get; set; }
        public double? Double { get; set; }
        public bool? Bool { get; set; }
        public Gender? Enum { get; set; }
    }

    [GraphQLArguments]
    public class GuidArgs
    {
        [Required]
        public Guid Id { get; set; }
    }
    [GraphQLArguments]
    public class GuidNonNullArgs
    {
        public Guid Id { get; set; }
    }

    [GraphQLArguments]
    public class RegexArgs
    {
        [RegularExpression("[^a]+", ErrorMessage = "Title does not match required format")]
        public string Title { get; set; }
        [RegularExpression("steve", ErrorMessage = "Author does not match required format")]
        public string Author { get; set; }
    }
    public class InputObject
    {
        public string Name { get; set; }
        public string LastName { get; set; }
        public DateTime? Birthday { get; set; }
    }

    public class ListOfObjectsWithIds
    {
        public IList<InputObjectId> InputObjects { get; set; }
    }

    public class InputObjectId
    {
        public int Id { get; set; }
        public long IdLong { get; set; }
    }

    [GraphQLArguments]
    public class FloatInput
    {
        public float Float { get; set; }
        public float? Float2 { get; set; }
    }
    [GraphQLArguments]
    public class DoubleInput
    {
        public double Double { get; set; }
        public double? Double2 { get; set; }
    }
    [GraphQLArguments]
    public class DecimalInput
    {
        public decimal Decimal { get; set; }
        public decimal? Decimal2 { get; set; }
    }

    [GraphQLArguments]
    public class ListOfGuidArgs
    {
        public List<Guid> Ids { get; set; }
    }
}