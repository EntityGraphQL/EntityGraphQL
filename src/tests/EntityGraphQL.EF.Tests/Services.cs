namespace EntityGraphQL.EF.Tests;

public class ProjectConfig(string type)
{
    public int Id { get; set; }
    public string Type { get; set; } = type;
}

public class AgeService
{
    public AgeService()
    {
        CallCount = 0;
    }

    public int CallCount { get; private set; }

    public async Task<int> GetAgeAsync(DateTime? birthday)
    {
        return await Task.Run(() => birthday.HasValue ? (int)(DateTime.Now - birthday.Value).TotalDays / 365 : 0);
    }

    public int GetAge(DateTime? birthday)
    {
        CallCount += 1;
        // you could do smarter things here like use other services
        return birthday.HasValue ? (int)(DateTime.Now - birthday.Value).TotalDays / 365 : 0;
    }
}

public class ConfigService
{
    public ConfigService()
    {
        CallCount = 0;
    }

    public int CallCount { get; set; }

    public ProjectConfig Get(int id)
    {
        CallCount += 1;
        return new ProjectConfig("Something");
    }

    public ProjectConfig[] GetList(int count, int from = 0)
    {
        CallCount += 1;
        var configs = new List<ProjectConfig>();
        for (int i = from; i < from + count; i++)
        {
            configs.Add(new ProjectConfig($"Something {i}") { Id = i });
        }
        return configs.ToArray();
    }
}
