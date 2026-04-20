namespace CodeTyper.Api.Services;

public static class ScoreCalculator
{
    public static double Calculate(int correctChars, double wpm, double accuracy, int missCount)
    {
        var baseScore = correctChars;
        var speedBonus = wpm * 2.0;
        var accuracyBonus = accuracy * 1.5;
        var missPenalty = missCount * 3.0;
        return Math.Round(baseScore + speedBonus + accuracyBonus - missPenalty, 2);
    }
}
