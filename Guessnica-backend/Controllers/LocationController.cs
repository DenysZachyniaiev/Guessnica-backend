using Guessnica_backend.Dtos.Location;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Guessnica_backend.Controllers;

[ApiController]
[Route("locations")]
[Produces("application/json")]
public class LocationController : ControllerBase
{
    private readonly ILocationService _locationService;

    public LocationController(ILocationService locationService)
    {
        _locationService = locationService;
    }
    
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAll()
    {
        var locations = await _locationService.GetAllAsync();
        var dtoList = locations.Select(loc => new LocationResponseDto
        {
            Id = loc.Id,
            Latitude = loc.Latitude,
            Longitude = loc.Longitude,
            ImageUrl = loc.ImageUrl,
            ShortDescription = loc.ShortDescription
        });

        return Ok(dtoList);
    }
    
    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> Get(int id)
    {
        var loc = await _locationService.GetByIdAsync(id);
        if (loc == null) return NotFound();

        return Ok(new LocationResponseDto
        {
            Id = loc.Id,
            Latitude = loc.Latitude,
            Longitude = loc.Longitude,
            ImageUrl = loc.ImageUrl,
            ShortDescription = loc.ShortDescription
        });
    }
    
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromForm] LocationCreateDto dto)
    {
        if (dto.Image == null || dto.Image.Length == 0)
            return BadRequest("Image is required");

        var loc = new Location
        {
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            ShortDescription = dto.ShortDescription
        };

        loc = await _locationService.CreateAsync(loc, dto.Image);

        return CreatedAtAction(nameof(Get), new { id = loc.Id }, new LocationResponseDto
        {
            Id = loc.Id,
            Latitude = loc.Latitude,
            Longitude = loc.Longitude,
            ImageUrl = loc.ImageUrl,
            ShortDescription = loc.ShortDescription
        });
    }
    
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromForm] LocationUpdateDto dto)
    {
        var updated = new Location
        {
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            ShortDescription = dto.ShortDescription
        };

        var loc = await _locationService.UpdateAsync(id, updated, dto.Image);

        if (loc == null) return NotFound();

        return Ok(new LocationResponseDto
        {
            Id = loc.Id,
            Latitude = loc.Latitude,
            Longitude = loc.Longitude,
            ImageUrl = loc.ImageUrl,
            ShortDescription = loc.ShortDescription
        });
    }
    
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _locationService.DeleteAsync(id);
        return success ? Ok() : NotFound();
    }
    
    [HttpPost("cleanup-images")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CleanupImages()
    {
        var removed = await _locationService.CleanupUnusedImagesAsync();
        return Ok(new { removed });
    }
}
