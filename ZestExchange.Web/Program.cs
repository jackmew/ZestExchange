using ZestExchange.Web;
using ZestExchange.Web.Components;
using StackExchange.Redis;
using ZestExchange.Repository;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add Repository (PostgreSQL)
builder.Services.AddZestRepository(builder.Configuration);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

// Add Orleans Client for real-time OrderBook (connects to Silo)
builder.UseOrleansClient(clientBuilder =>
{
    var connectionString = builder.Configuration.GetConnectionString("redis");

    if (string.IsNullOrEmpty(connectionString))
    {
        clientBuilder.UseLocalhostClustering();
    }
    else
    {
        clientBuilder.UseRedisClustering(options => options.ConfigurationOptions = ConfigurationOptions.Parse(connectionString));
    }

    // Same stream provider as Silo for subscribing to OrderBook updates
    clientBuilder.AddMemoryStreams("OrderBookProvider");
});

builder.Services.AddHttpClient<WeatherApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
