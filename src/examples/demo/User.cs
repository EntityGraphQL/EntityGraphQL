using System.Collections.Generic;
using System.Linq;

namespace demo;

public class User
{
    public uint Id { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
}

// This could fetch users from some external API or other system
public class UserService
{
    public IEnumerable<User> GetUsers()
    {
        return new List<User>
        {
            new User
            {
                Id = 1,
                Name = "John",
                Email = "john@example.com",
            },
            new User
            {
                Id = 2,
                Name = "Jane",
                Email = "jane@example.com",
            },
            new User
            {
                Id = 3,
                Name = "Bob",
                Email = "bob@example.com",
            },
        };
    }

    public User? GetUser(uint createdBy)
    {
        return GetUsers().FirstOrDefault(u => u.Id == createdBy);
    }

    /// <summary>
    /// Fetch many users at once. Used with .ResolveBulk() so selecting the contributor of every movie in a
    /// list is a single call instead of one per movie (the N+1 the EGQL001 analyzer warns about).
    /// </summary>
    public IDictionary<uint, User> GetUsers(IEnumerable<uint> ids)
    {
        var wanted = ids.ToHashSet();
        return GetUsers().Where(u => wanted.Contains(u.Id)).ToDictionary(u => u.Id);
    }
}
