using BISTApp.Data;
using BISTApp.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Port ayarı (Render için)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // JSON property names için camelCase kullan
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "BIST API", Version = "v1" });
});

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrWhiteSpace(defaultConnection) &&
    (defaultConnection.Contains("Host=") || defaultConnection.Contains("Username=") || defaultConnection.Contains("Password=")))
{
    builder.Services.AddDbContext<BistDbContext>(options =>
        options.UseNpgsql(defaultConnection));
}
else
{
    builder.Services.AddDbContext<BistDbContext>(options =>
        options.UseSqlite(defaultConnection));
}

// HttpClients
builder.Services.AddScoped<FileSymbolService>();

builder.Services.AddHttpClient<YahooFinanceClient>();

// Data service
builder.Services.AddScoped<BistDataService>();

// Background Service
builder.Services.AddHostedService<StockUpdateBackgroundService>();

var app = builder.Build();

// Database migration
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<BistDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "BIST API v1");
});

app.UseHttpsRedirection();

// Default file (index.html) - UseStaticFiles'den ÖNCE olmalı
app.UseDefaultFiles();

// Static files (wwwroot)
app.UseStaticFiles();

app.UseAuthorization();

app.MapControllers();

app.Run();
