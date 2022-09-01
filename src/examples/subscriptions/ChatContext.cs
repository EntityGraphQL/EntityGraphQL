using Microsoft.EntityFrameworkCore;

#nullable disable
public class ChatContext : DbContext
{
    public DbSet<Message> Messages { get; set; }
    public DbSet<User> Users { get; set; }
    public ChatContext(DbContextOptions<ChatContext> options)
        : base(options)
    {
    }
}

public class Message
{
    public Guid Id { get; set; }
    public string Text { get; set; }
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; }
    public User User { get; set; }
}

public class User
{
    public string Id { get; set; }
    public string Name { get; set; }
}
#nullable enable