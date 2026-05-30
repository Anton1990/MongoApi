using MongoApi.Infrastructure;
using MongoApi.Services;
using MongoApi.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<ProductService>();
builder.Services.AddSingleton<CustomerService>();
builder.Services.AddSingleton<DatabaseInitializer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var initializer = app.Services.GetRequiredService<DatabaseInitializer>();
await initializer.InitializeAsync();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
