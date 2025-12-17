namespace Guessnica_backend.Services.Helpers;

public class HaversineDistance
{
    public static double CalculateDistance(
        decimal lat1, decimal lon1,
        decimal lat2, decimal lon2
    )
    {
        const double R = 6371000;
        
        double dLat = DegreesToRadians((double)(lat2 - lat1));
        double dLon = DegreesToRadians((double)(lon2 - lon1));

        double lat1Rad = DegreesToRadians((double)lat1);
        double lat2Rad = DegreesToRadians((double)lat2);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    public static double DegreesToRadians(double deg) => deg * Math.PI / 180.0;
}