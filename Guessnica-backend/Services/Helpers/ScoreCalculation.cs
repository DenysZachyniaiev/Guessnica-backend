namespace Guessnica_backend.Services.Helpers;

public class ScoreCalculation
{
    public static int CalculateScore(int basePoints, double distanceMeters, int timeSeconds, double maxDistance)
    {
        if (distanceMeters > maxDistance)
            return 0;
        
        double distanceFactor = 1.0 - (distanceMeters / maxDistance);
        double timeFactor = 1.0 / (1 + timeSeconds / 60);
        return (int)Math.Round(basePoints * distanceFactor * timeFactor, 2);
    }
}