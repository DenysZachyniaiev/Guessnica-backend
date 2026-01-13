using Guessnica_backend.Data;
using Guessnica_backend.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddGuessnicaControllers()
    .AddGuessnicaDb(builder.Configuration)
    .AddGuessnicaApiBehavior()
    .AddGuessnicaIdentity()
    .AddGuessnicaAuth(builder.Configuration)
    .AddGuessnicaSwagger()
    .AddGuessnicaCors(builder.Configuration)
    .AddGuessnicaHealthChecks()
    .AddGuessnicaAppServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler("/error");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.DocumentTitle = "Guessnica API";
        c.DisplayRequestDuration();
    });
}

app.UseStaticFiles();

app.UseCors(app.Environment.IsDevelopment() ? "DevCors" : "ProdCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Map("/error", (HttpContext httpContext) =>
{
    var feature = httpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
    var ex = feature?.Error;
    return Results.Problem(
        title: "Unexpected error",
        detail: app.Environment.IsDevelopment() ? ex?.ToString() : null,
        statusCode: StatusCodes.Status500InternalServerError
    );
});

await SeedData.EnsureSeedDataAsync(app.Services);

app.Run();