using WebApplication1;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .WithName("GetWeatherForecast")
    .WithOpenApi();

app.Run();

/*Let's imagine there is an API: (string location) -> geoapi.google.com -> GeoData
Every request to this API cost 1$
We need to build system handling 100000000 of this requests daily
but we findout that for majority of users location is the same and GeoData object returned to same key is also the same
    So our goal is to build endpoint which minimizing our expenses*/

app.MapGet("/getGeoData", async (string location, ICache cache) =>
    {
        var geoData = cache.Get(location);

        if (geoData != null)
        {
            return geoData;
        }

        var client = new GeoDataClient();
        var semaphore = new SemaphoreSlim(0,1);
        
        semaphore.Wait();
        try
        {
            geoData = cache.Get(location);
            if (geoData == null)
            {
                geoData = client.GetDataFromGoogle(location);
                cache.Set(location, geoData);
            }
        }
        finally {
            semaphore.Release();
        }
        
        return geoData;
    })
    .WithName("getGeoData")
    .WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}