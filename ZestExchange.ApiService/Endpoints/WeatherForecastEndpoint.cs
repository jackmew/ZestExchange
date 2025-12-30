using FastEndpoints;

namespace ZestExchange.ApiService.Endpoints;

public class WeatherForecastEndpoint : EndpointWithoutRequest<WeatherForecast[]>
{
    private readonly ILogger<WeatherForecastEndpoint> _logger;

    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    public WeatherForecastEndpoint(ILogger<WeatherForecastEndpoint> logger)
    {
        _logger = logger;
    }

    public override void Configure()
    {
        Get("/weatherforecast");
        AllowAnonymous();
    }

    public override Task HandleAsync(CancellationToken ct)
    {
        _logger.LogInformation("Enter weatherforecast endpoint");

        Response = Enumerable.Range(1, 5).Select(index =>
            new WeatherForecast
            (
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]
            ))
            .ToArray();

        return Task.CompletedTask;
    }
}

public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
