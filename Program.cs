using Microsoft.OpenApi;
using MongoApi.GraphQL;
using MongoApi.Infrastructure;
using MongoApi.Infrastructure.Authentication;
using MongoApi.Infrastructure.Authorization;
using MongoApi.Infrastructure.Exceptions;
using MongoApi.Messaging;
using MongoApi.Services;
using MongoApi.Services.Abstractions;
using MongoApi.Settings;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDbSettings"));
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();
builder.Services.AddHostedService<OutboxPublisher>();
builder.Services.AddSingleton<IProductService, ProductService>();
builder.Services.AddSingleton<ICustomerService, CustomerService>();
builder.Services.AddSingleton<ICategoryService, CategoryService>();
builder.Services.AddSingleton<IStoreService, StoreService>();
builder.Services.AddSingleton<OrderService>();
builder.Services.AddSingleton<DatabaseInitializer>();

// Auth: JWT Bearer → ClaimsPrincipal
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication("HeaderAuth")
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, HeaderAuthHandler>(
        "HeaderAuth", null);

builder.Services.AddAuthorization();

builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddSingleton<IRoleService, RoleService>();
builder.Services.AddSingleton<IUserService, UserService>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Вставьте JWT токен. Получить через POST /api/auth/login."
    });

    options.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>() }
    });
});

builder.Services
    .AddGraphQLServer()
    .AddQueryType<ProductQuery>()
    .AddTypeExtension<OrganizationQuery>()
    .AddMutationType<ProductMutation>()
    .AddTypeExtension<OrganizationMutation>()
    .AddTypeExtension<ProductExtensions>()
    .AddDataLoader<StoresByProductIdDataLoader>()
    .AddErrorFilter<GraphQLErrorFilter>()
    .AddAuthorization()
    .AddFiltering()
    .AddSorting()
    .AddProjections();

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

app.UseExceptionHandler(); // ← первым — ловит все необработанные исключения REST

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseHttpMetrics();
app.MapControllers();
app.MapMetrics();
app.MapGraphQL(); // endpoint: /graphql (Banana Cake Pop UI включён автоматически)

app.Run();
