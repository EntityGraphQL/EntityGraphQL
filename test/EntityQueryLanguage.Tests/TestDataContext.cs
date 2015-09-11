using System.Linq;
using System.Collections.Generic;
using System;

// This is a mock datamodel, what would be your real datamodel and EF context
namespace EntityQueryLanguage.Tests
{
    internal class TestDataContext {
		public IEnumerable<Project> Projects { get; set; }
		public IEnumerable<Task> Tasks { get; set; }
		public IEnumerable<Location> Locations { get; set; }
		public IEnumerable<Person> People { get; set; }
	}
	
	internal class Person {
		public int Id { get; set; }
		public string Name { get; set; }
		public string LastName { get; set; }
	}
	
	internal class Project {
		public int Id { get; set; }
		public int Type { get; set; }
		public Location Location { get; set; }
		public IEnumerable<Task> Tasks { get; set; }
		public Person Owner { get; set; }
	}
	
	internal class Task {
		public int Id { get; set; }
		public string Name { get; set; }
		public bool IsActive { get; set; }
		public Person Assignee { get; set; }
	}
	internal class Location {
		public int Id { get; set; }
		public string Address { get; set; }
		public string State { get; set; }
		public string Country { get; set; }
		public string Planet { get; set; }
	}
	

	// this is a schema that maps back to your current data model, helping you version APIs. You can change your current data model 
	// and keep the API valid by continuing to return the expected objects and fields.
	//
	// The key here is that when you change the underlying data model and entities you get a compile error, fixing them to return what is expected
	// of these classes means you can make non-breaking changes to your exposed API
	namespace ApiVersion1
    {
        internal class TestObjectGraphSchema : MappedSchemaProvider {
			public TestObjectGraphSchema() {				
				// we define each type we and their fields
				
				// Without the fields argument we expose Location fields as-is. Easy and simple, but this means changes in 
				// Location may break API
				Type<Location>(name: "location", description: "A geographical location");
				
				// It's better to define the fields of each type you want to expose, so over time your data model can change and 
				// if you keep these definitions compiling you shouldn't break any API calls
				Type<Person>(name: "person", description: "Details of a person in the system", fields: new {
					// you don't need to define the return type unless you need to specify the type to map to
					Id = Field((Person p) => p.Id, "The unique identifier"),
					FirstName = Field((Person p) => p.Name, "Person's first name"),
					LastName = Field((Person p) => p.LastName, "Person's last name"),
				});
				
				Type<Project>("project", "Details of a project", new {
					Id = Field((Project p) => p.Id, "Unique identifier for the project"),
					Name = Field((Project p) => p.Owner.Name + "'s Project", "Project's name"), // fields can be built with expressions
					// we will auto map to the defined location type (Type<Location>) as there is only one defined for Location
					Location = Field((Project p) => p.Location, "The location of the project"),
					
					// If you need to define that the return type is one defined in this schema you need to create an EntityField object
					OpenTasks = Field((Project p) => p.Tasks.Where(t => t.IsActive), "All open tasks for the project", "openTask"),
					ClosedTasks = Field((Project p) => p.Tasks.Where(t => !t.IsActive), "All closed tasks for the project", "closedTask"),
				});
				
				// You can define multiple types from one base type and define a filter which is applied
				Type<Task>("openTask", "Details of a project", new {
					Id = Field((Task t) => t.Id, "Unique identifier for a task"),
					Description = Field((Task t) => t.Name, "Description of the task"),
				});
				Type<Task>("closedTask", "Details of a project", new {
					Id = Field((Task t) => t.Id, "Unique identifier for a task"),
					Description = Field((Task t) => t.Name, "Description of the task"),
				});
				
				// Now we defined what fields are at the root of the query graph
				Query<TestDataContext>(new {
					Locations = Field((TestDataContext db) => db.Locations, "All locations in the world", "location"),
					People = Field((TestDataContext db) => db.People, "Person details", "person"),
					PublicProjects = Field((TestDataContext db) => db.Projects.Where(p => p.Type == 2), "All projects marked as public", "project"),
					PrivateProjects = Field((TestDataContext db) => db.Projects.Where(p => p.Type == 1), "All privately held projects", "project"),
					OpenTasks = Field((TestDataContext db) => db.Tasks.Where(t => t.IsActive), "All open tasks for all projects", "openTask"),
					ClosedTasks = Field((TestDataContext db) => db.Tasks.Where(t => !t.IsActive), "All closedtasks for all projects", "closedTask"),
					DefaultLocation = Field((TestDataContext db) => db.Locations.First(l => l.Id == 10), "The default location for projects", "location")
				});
			}
        }
	}
}