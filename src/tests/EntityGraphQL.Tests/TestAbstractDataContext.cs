using System.Collections.Generic;
using System;
using Microsoft.EntityFrameworkCore;

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
    }

    public abstract class Animal
    {
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
}