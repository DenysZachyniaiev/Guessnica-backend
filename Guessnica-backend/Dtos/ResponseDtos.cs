namespace Guessnica_backend.Dtos
{
    public class MessageResponseDto
    {
        public string Message { get; set; }
    }

    public class ErrorResponseDto
    {
        public IEnumerable<string> Errors { get; set; }
    }
}