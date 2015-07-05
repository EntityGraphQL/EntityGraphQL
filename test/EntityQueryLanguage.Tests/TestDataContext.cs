using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;

// This is a mock datamodel, what would be your real datamodel and EF context
namespace EntityQueryLanguage.Tests {
	internal class TestSchema {
		public IEnumerable<Project> Projects { get; set; }
		public IEnumerable<Task> Tasks { get; set; }
		public IEnumerable<Location> Locations { get; set; }
		public IEnumerable<Person> Persons { get; set; }
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
		// See here we choose to expose projects as public and private entities in the "API"
		internal class ApiSchemaVersion1 {
			public PublicProject PublicProjects { get; }
			public PrivateProject PrivateProjects { get; }
			public IEnumerable<ApiTask> Tasks { get; }
		}
		// use DataQueryClassMapping to tell EQL what real entity this maps to. Only the fields specifically declared here are exposed
		internal class PublicProject : ClassMapping<Project> {
			// we can applie filters to the base entites to only return what we want
			public PublicProject() : base(p => p.Type == 1) {
			}
			// nothing extra here, it will map to Id on the base entity
			public int Id { get; set; }
			public string Name { get; set; }
			// Here we are creating a field not on the base entity - of course nothing stops you form having an OpenTasks on the base entity
			// this just means you can freely change the base entity and fix the compile error here to keep the API stable 
			public CollectionFieldMapping<Project, Task> OpenTasks = new CollectionFieldMapping<Project, Task>(p => p.Tasks.Where(t => t.IsActive));
			public CollectionFieldMapping<Project, Task> ClosedTasks = new CollectionFieldMapping<Project, Task>(p => p.Tasks.Where(t => !t.IsActive));
			// We don't include Type field here so it is not accessable in the API schema - allows you to not expose everything
		}
		internal class PrivateProject : ClassMapping<Project> {
			public PrivateProject() : base(p => p.Type == 2) {
			}
			public int Id { get; set; }
			public string Name { get; set; }
			public CollectionFieldMapping<Project, Task> OpenTasks = new CollectionFieldMapping<Project, Task>(p => p.Tasks.Where(t => t.IsActive));
			public CollectionFieldMapping<Project, Task> ClosedTasks = new CollectionFieldMapping<Project, Task>(p => p.Tasks.Where(t => !t.IsActive));
		}
		// All items that 
		internal class ApiTask : ClassMapping<Task> {
			public int Id { get; set; }
			// Creating a field that might describe things externally better than an internal name
			public FieldMapping<Task, string> Description = new FieldMapping<Task, string>(t => t.Name);
		}
	}
	
	
	public class ClassMapping<TContext> {
		public Expression<Func<TContext, bool>> MappingPredicate { get; private set; }
		public ClassMapping() {
		}
		public ClassMapping(Expression<Func<TContext, bool>> mappingPredicate) {
			MappingPredicate = mappingPredicate;
		}
	}
	
	public class CollectionFieldMapping<TContext, TFieldType> {
		public Expression<Func<TContext, IEnumerable<TFieldType>>> MappingQuery {get; private set; }
		public CollectionFieldMapping(Expression<Func<TContext, IEnumerable<TFieldType>>> mappingQuery) {
			MappingQuery = mappingQuery;
		}
	}
	
	public class FieldMapping<TContext, TFieldType> {
		public Expression<Func<TContext, TFieldType>> MappingQuery {get; private set; }
		public FieldMapping(Expression<Func<TContext, TFieldType>> mappingQuery) {
			MappingQuery = mappingQuery;
		}
	}
}