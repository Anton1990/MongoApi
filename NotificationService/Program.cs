using MongoDB.Driver;
using NotificationService.Consumers;
using NotificationService.Models;

var builder = WebApplication.CreateBuilder(args);

// MongoDB
var connectionString = builder.Configuration["MongoDb:ConnectionString"]
    ?? throw new InvalidOperationException("MongoDb:ConnectionString is required");
var databaseName = builder.Configuration["MongoDb:DatabaseName"] ?? "NotificationsDb";

var mongoClient = new MongoClient(connectionString);
var database = mongoClient.GetDatabase(databaseName);
var notificationsCollection = database.GetCollection<Notification>("notifications");

builder.Services.AddSingleton(notificationsCollection);

// RabbitMQ consumer (BackgroundService)
builder.Services.AddHostedService<ProductCreatedConsumer>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

app.Run();
