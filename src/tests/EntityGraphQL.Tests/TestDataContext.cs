using System;
using System.Collections.Generic;
using System.Linq;
using EntityGraphQL.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace EntityGraphQL.Tests;

/// <summary>
/// This is a mock datamodel, what would be your real datamodel and/or EF context
///
/// Used by most of the tests
/// </summary>
public class TestDataContext
{
    [GraphQLIgnore]
    private List<Project> projects = [];

    public int TotalPeople => People.Count;

    [Obsolete("This is obsolete, use Projects instead")]
    public IEnumerable<ProjectOld> ProjectsOld { get; set; } = [];
    public List<Project> Projects
    {
        get => projects;
        set => projects = value;
    }
    public IQueryable<Project> QueryableProjects
    {
        get => projects.AsQueryable();
        set => projects = value.ToList();
    }
    public virtual IEnumerable<Task> Tasks { get; set; } = [];
    public List<Location> Locations { get; set; } = [];
    public virtual List<Person> People { get; set; } = [];
    public List<User> Users { get; set; } = [];
    public System.Threading.Tasks.Task<int?> FirstUserId => System.Threading.Tasks.Task.FromResult(Users.FirstOrDefault()?.Id);
    public Project MainProject => projects.First();
}

public class TestDataContext2
{
    public List<Task> Tasks { get; set; } = [];
    public List<Person> People { get; set; } = [];
    public List<User> Users { get; set; } = [];
}

public class ProjectOld { }

[JsonConverter(typeof(StringEnumConverter))]
public enum Gender
{
    Female,
    Male,
    NotSpecified,
}

public enum UserType
{
    Admin,
    User,
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Field1 { get; set; }
    public string Field2 { get; set; } = string.Empty;
    public Person? Relation { get; set; }
    public Task? NestedRelation { get; set; }
    public Task[] Tasks { get; set; } = [];
    public int? RelationId { get; set; }
}

public class Person
{
    public int Id { get; set; }

    [GraphQLIgnore(GraphQLIgnoreType.Input)]
    public Guid Guid { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Person? Manager { get; set; }
    public Gender Gender { get; set; }
    public List<Project> Projects { get; set; } = [];
    public List<Task> Tasks { get; set; } = [];
    public DateTime? Birthday { get; set; }
    public User? User { get; set; }
    public double Height { get; set; }
    public byte[]? Image { get; set; }

    // fake an error
    public string Error
    {
        get => throw new EntityGraphQLException("Field failed to execute", new Dictionary<string, object> { { "code", 1 } });
        set => throw new Exception("Field failed to execute");
    }

    public string Error_UnexposedException
    {
        get => throw new Exception("You should not see this message outside of Development");
        set => throw new Exception("You should not see this message outside of Development");
    }

    public string Error_UnexposedArgumentException
    {
        get => throw new ArgumentException("You should not see this message outside of Development");
        set => throw new ArgumentException("You should not see this message outside of Development");
    }

    public string Error_AggregateException
    {
        get => throw new AggregateException(Enumerable.Range(0, 2).Select(_ => new Exception("You should not see this message outside of Development")));
        set => throw new AggregateException(Enumerable.Range(0, 2).Select(_ => new Exception("You should not see this message outside of Development")));
    }
    public string Error_Allowed => throw new TestException();

    public double GetHeight(HeightUnit unit)
    {
        return unit switch
        {
            HeightUnit.Cm => Height,
            HeightUnit.Meter => Height / 100,
            HeightUnit.Feet => Height * 0.0328,
            _ => throw new NotSupportedException($"Height unit {unit} not supported"),
        };
    }
}

[AllowedException]
public class TestException : Exception
{
    public TestException()
        : base("This error is allowed") { }
}

public class Project
{
    public int Id { get; set; }
    public char Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public Location? Location { get; set; }
    public IEnumerable<Task> Tasks { get; set; } = [];
    public Person? Owner { get; set; }
    public int CreatedBy { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset? Created { get; set; }
    public DateTime? Updated { get; set; }
    public IEnumerable<Project> Children { get; set; } = [];

    [GraphQLField]
    public IEnumerable<Task> SearchTasks(string name)
    {
        return Tasks.Where(t => t.Name.Contains(name));
    }
}

public class Task
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public Person? Assignee { get; set; }
    public Project? Project { get; set; }
    public float HoursEstimated { get; set; }
    public float HoursCompleted { get; set; }
}

public class Location
{
    public int Id { get; set; }
    public string Address { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Planet { get; set; } = string.Empty;
}

public enum HeightUnit
{
    Cm,
    Meter,
    Feet,
}

internal static class DataFiller
{
    internal static T FillWithTestData<T>(this T context)
        where T : TestDataContext
    {
        var user = new User
        {
            Id = 100,
            Field1 = 2,
            Field2 = "2",
            Relation = new Person(),
            NestedRelation = new Task(),
        };
        context.Users = [user];

        var project = new Project
        {
            Id = 55,
            Name = "Project 3",
            Tasks = [new Task { Id = 1, Name = "task 1" }, new Task { Id = 2, Name = "task 2" }, new Task { Id = 3, Name = "task 3" }, new Task { Id = 4, Name = "task 4" }],
            Created = DateTimeOffset.Now.AddMonths(-3),
            Updated = DateTime.Now.AddMonths(-2),
        };
        context.People = [MakePerson(99, user, project)];
        context.Projects = [project];
        return context;
    }

    public static Person MakePerson(int id, User? user, Project? project)
    {
        return new Person
        {
            Id = id,
            Guid = new Guid("cccccccc-bbbb-4444-1111-ccddeeff0033"),
            Name = "Luke",
            LastName = "Last Name",
            Birthday = new DateTime(2000, 1, 1, 1, 1, 1, 1),
            User = user,
            Height = 183,
            Gender = Gender.Male,
            Projects = project != null ? [project] : [],
        };
    }
}
