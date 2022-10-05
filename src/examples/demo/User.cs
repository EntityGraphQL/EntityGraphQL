using System.Collections.Generic;
using System.Linq;

namespace demo;

public class User
{
    public uint Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// This could fetch users from some external API or other system
public class UserService
{
    public IEnumerable<User> GetUsers()
    {
        return new List<User>
        {
            new User { Id = 1, Name = "John", Email = "john@example.com" },
            new User { Id = 2, Name = "Jane", Email = "jane@example.com" },
            new User { Id = 3, Name = "Bob", Email = "bob@example.com" },
        };
    }

    public User GetUser(uint createdBy)
    {
        return GetUsers().FirstOrDefault(u => u.Id == createdBy);
    }
}