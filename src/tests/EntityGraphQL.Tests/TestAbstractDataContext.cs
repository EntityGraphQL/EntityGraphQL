using EntityGraphQL.Schema;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace EntityGraphQL.Tests
{
    /// <summary>
    /// This is a mock datamodel, what would be your real datamodel and/or EF context
    ///
    /// Used by most of the tests
    /// </summary>
    public class TestAbstractDataContext
    {
        public List<Animal> Animals { get; set; } = new List<Animal>();
        public List<Cat> Cats { get; set; } = new List<Cat>();
        public List<Dog> Dogs { get; set; } = new List<Dog>();

        public List<PersonType> People { get; set; } = new List<PersonType>();

        [GraphQLMutation]
        public Expression<Func<TestAbstractDataContext, Animal>> TestMutation(int id)
        {
            return (db) => db.Animals.First();
        }
    }

    public class TestAbstractDataContextNoAnimals
    {
        public List<Cat> Cats { get; set; } = new List<Cat>();
        public List<Dog> Dogs { get; set; } = new List<Dog>();
    }

    public class TestUnionDataContext
    {
        public List<IAnimal> Animals { get; set; } = new List<IAnimal>();
    }

    public interface IAnimal
    {

    }

    public abstract class Animal : IAnimal
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }
    public class Cat : Animal
    {
        public int Lives { get; set; }
    }

    public class Dog : Animal
    {
        public bool HasBone { get; set; }
    }

    public class PersonType
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public DateTime? Birthday { get; set; }
        public IEnumerable<Dog> Dogs { get; set; }
    }
}