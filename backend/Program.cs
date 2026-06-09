using CashDash.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();

builder.Services.AddHttpClient<IRouteCalculator, RouteCalculator>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "https://cashdash-5r6.pages.dev/")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IRouteCalculator, RouteCalculator>();

var app = builder.Build();
app.UseCors("AllowAngular");
app.MapControllers();

app.Run();