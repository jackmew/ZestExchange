using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.Swagger;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Add Orleans Client (連接 Silo)
builder.UseOrleansClient(clientBuilder =>
{
    clientBuilder.UseLocalhostClustering();
});

// Add FastEndpoints
builder.Services.AddFastEndpoints();

// Add Swagger
builder.Services.SwaggerDocument(o =>
{
    o.DocumentSettings = s =>
    {
        s.Title = "ZestExchange API";
        s.Version = "v1";
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

// Use FastEndpoints
app.UseFastEndpoints(c =>
{
    c.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
});

// Use Swagger UI
app.UseSwaggerGen();

app.MapDefaultEndpoints();

app.Run();
