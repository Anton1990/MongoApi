using MongoApi.Infrastructure;
using MongoApi.Messaging;
using MongoApi.Services;
using MongoApi.Settings;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddSingleton<ProductService>();
builder.Services.AddSingleton<CustomerService>();
builder.Services.AddSingleton<CategoryService>();
builder.Services.AddSingleton<OrderService>();
builder.Services.AddSingleton<DatabaseInitializer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var initializer = app.Services.GetRequiredService<DatabaseInitializer>();
try
{
    await initializer.InitializeAsync();
}
catch (Exception ex)
{
    // MongoDB недоступна при старте — приложение продолжает работу.
    // Индексы будут созданы при следующем рестарте когда MongoDB будет готова.
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "DatabaseInitializer failed at startup. Indexes may not be created.");
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseHttpMetrics();
app.MapControllers();
app.MapMetrics();

app.Run();
