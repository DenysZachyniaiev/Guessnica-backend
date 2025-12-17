namespace Guessnica_backend.Dtos.Location
{
    public class LocationResponseDto
    {
        public int Id { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public required string ImageUrl { get; set; }
        public string? ShortDescription { get; set; }
    }
}