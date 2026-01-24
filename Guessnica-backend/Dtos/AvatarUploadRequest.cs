using Microsoft.AspNetCore.Http;

namespace Guessnica_backend.Dtos;

public class AvatarUploadRequest
{
    public IFormFile Avatar { get; set; } = default!;
}