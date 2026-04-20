namespace CodeTyper.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!db.Words.Any())
        {
            db.Words.AddRange(
                new WordEntity { WordId = Guid.NewGuid(), Word = "public", Language = "java", Difficulty = "easy", Weight = 10, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "interface", Language = "java", Difficulty = "normal", Weight = 8, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "synchronized", Language = "java", Difficulty = "hard", Weight = 3, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "lambda", Language = "python", Difficulty = "easy", Weight = 10, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "decorator", Language = "python", Difficulty = "hard", Weight = 4, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "function", Language = "javascript", Difficulty = "easy", Weight = 10, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "promise", Language = "javascript", Difficulty = "normal", Weight = 8, Enabled = true }
            );
        }

        if (!db.Users.Any(u => u.UserId == "demo-user"))
        {
            db.Users.Add(new UserEntity
            {
                UserId = "demo-user",
                Email = "demo@example.com",
                DisplayName = "demo",
                TeamId = "team-a",
                GlobalAlias = "DemoUser"
            });
        }

        await db.SaveChangesAsync();
    }
}
