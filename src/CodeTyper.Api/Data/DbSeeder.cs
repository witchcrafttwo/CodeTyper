namespace CodeTyper.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!db.Words.Any())
        {
            db.Words.AddRange(
                // Java
                new WordEntity { WordId = Guid.NewGuid(), Word = "public", Language = "java", Difficulty = "easy", Weight = 10, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "class", Language = "java", Difficulty = "easy", Weight = 10, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "static", Language = "java", Difficulty = "easy", Weight = 9, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "void", Language = "java", Difficulty = "easy", Weight = 9, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "interface", Language = "java", Difficulty = "normal", Weight = 8, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "abstract", Language = "java", Difficulty = "normal", Weight = 7, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "implements", Language = "java", Difficulty = "normal", Weight = 7, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "synchronized", Language = "java", Difficulty = "hard", Weight = 3, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "transient", Language = "java", Difficulty = "hard", Weight = 3, Enabled = true },
                // Python
                new WordEntity { WordId = Guid.NewGuid(), Word = "def", Language = "python", Difficulty = "easy", Weight = 10, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "import", Language = "python", Difficulty = "easy", Weight = 10, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "lambda", Language = "python", Difficulty = "easy", Weight = 9, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "yield", Language = "python", Difficulty = "normal", Weight = 7, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "generator", Language = "python", Difficulty = "normal", Weight = 6, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "decorator", Language = "python", Difficulty = "hard", Weight = 4, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "metaclass", Language = "python", Difficulty = "hard", Weight = 3, Enabled = true },
                // JavaScript
                new WordEntity { WordId = Guid.NewGuid(), Word = "function", Language = "javascript", Difficulty = "easy", Weight = 10, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "const", Language = "javascript", Difficulty = "easy", Weight = 10, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "let", Language = "javascript", Difficulty = "easy", Weight = 9, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "promise", Language = "javascript", Difficulty = "normal", Weight = 8, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "async", Language = "javascript", Difficulty = "normal", Weight = 8, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "await", Language = "javascript", Difficulty = "normal", Weight = 8, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "prototype", Language = "javascript", Difficulty = "hard", Weight = 4, Enabled = true },
                new WordEntity { WordId = Guid.NewGuid(), Word = "WeakMap", Language = "javascript", Difficulty = "hard", Weight = 3, Enabled = true }
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
