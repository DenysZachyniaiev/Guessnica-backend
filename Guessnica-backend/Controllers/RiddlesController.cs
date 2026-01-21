using Guessnica_backend.Dtos.Riddle;
using Guessnica_backend.Models;
using Guessnica_backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Guessnica_backend.Controllers;

[ApiController]
[Route("riddles")]
[Produces("application/json")]
public class RiddlesController : ControllerBase
{
    private readonly IRiddleService _service;

    public RiddlesController(IRiddleService service)
    {
        _service = service;
    }
    
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll()
    {
        var items = await _service.GetAllAsync();

        var result = items.Select(r => new RiddleResponseDto
        {
            Id = r.Id,
            Description = r.Description,
            Difficulty = (int)r.Difficulty,
            LocationId = r.LocationId,
            ShortDescription = r.Location.ShortDescription,
            Latitude = r.Location.Latitude,
            Longitude = r.Location.Longitude,
            ImageUrl = r.Location.ImageUrl,
            TimeLimitSeconds = r.TimeLimitSeconds,
            MaxDistanceMeters = r.MaxDistanceMeters
        });

        return Ok(result);
    }
    
    [HttpGet("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Get(int id)
    {
        var riddle = await _service.GetByIdAsync(id);
        if (riddle == null) return NotFound();

        return Ok(new RiddleResponseDto
        {
            Id = riddle.Id,
            Description = riddle.Description,
            Difficulty = (int)riddle.Difficulty,
            LocationId = riddle.LocationId,
            ShortDescription = riddle.Location.ShortDescription,
            Latitude = riddle.Location.Latitude,
            Longitude = riddle.Location.Longitude,
            ImageUrl = riddle.Location.ImageUrl,
            TimeLimitSeconds = riddle.TimeLimitSeconds,
            MaxDistanceMeters = riddle.MaxDistanceMeters
        });
    }
    
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] RiddleCreateDto dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var riddle = new Riddle
        {
            Description = dto.Description,
            Difficulty = (RiddleDifficulty)dto.Difficulty,
            LocationId = dto.LocationId,
            TimeLimitSeconds = dto.TimeLimitSeconds,
            MaxDistanceMeters = dto.MaxDistanceMeters
        };

        var created = await _service.CreateAsync(riddle);

        if (created == null)
        {
            return Problem(
                statusCode: 400,
                title: "Invalid location",
                detail: "Location not found"
            );
        }

        return CreatedAtAction(nameof(Get), new { id = created.Id }, new RiddleResponseDto
        {
            Id = created.Id,
            Description = created.Description,
            Difficulty = (int)created.Difficulty,
            LocationId = created.LocationId,
            ShortDescription = created.Location.ShortDescription,
            Latitude = created.Location.Latitude,
            Longitude = created.Location.Longitude,
            ImageUrl = created.Location.ImageUrl,
            TimeLimitSeconds = created.TimeLimitSeconds,
            MaxDistanceMeters = created.MaxDistanceMeters
        });
    }
    
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] RiddleUpdateDto dto)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var updatedModel = new Riddle
        {
            Description = dto.Description,
            Difficulty = (RiddleDifficulty)dto.Difficulty,
            LocationId = dto.LocationId,
            TimeLimitSeconds = dto.TimeLimitSeconds,
            MaxDistanceMeters = dto.MaxDistanceMeters
        };

        Riddle? updated;

        try
        {
            updated = await _service.UpdateAsync(id, updatedModel);
        }
        catch (InvalidOperationException)
        {
            return Problem(
                statusCode: 400,
                title: "Invalid location",
                detail: "Location not found"
            );
        }

        if (updated == null)
            return NotFound();
        
        var withLocation = await _service.GetByIdAsync(updated.Id);
        if (withLocation == null) return NotFound();

        return Ok(new RiddleResponseDto
        {
            Id = withLocation.Id,
            Description = withLocation.Description,
            Difficulty = (int)withLocation.Difficulty,
            LocationId = withLocation.LocationId,
            ShortDescription = withLocation.Location.ShortDescription,
            Latitude = withLocation.Location.Latitude,
            Longitude = withLocation.Location.Longitude,
            ImageUrl = withLocation.Location.ImageUrl,
            TimeLimitSeconds = withLocation.TimeLimitSeconds,
            MaxDistanceMeters = withLocation.MaxDistanceMeters
        });
    }
    
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var ok = await _service.DeleteAsync(id);
        return ok ? Ok() : NotFound();
    }
}