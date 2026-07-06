using System.Reflection;
using Core.Migrator;
using Core.CQRS;
using Core.CQRS.Decorators;
using Core.DataAccessTypes;
using Core.Infrastructure;
using Core.KeycloakService;
using Core.Logger;
using Core.Observability;
using Chatter.Users.DataAccess;
using Core.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var keycloakConfiguration = builder.Configuration.GetSection("Keycloak");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakConfiguration["Authority"];
        options.Audience = keycloakConfiguration["Audience"];

        options.RequireHttpsMetadata = keycloakConfiguration["RequireHttpsMetadata"] != null
            ? keycloakConfiguration["RequireHttpsMetadata"]!.Equals("true", StringComparison.OrdinalIgnoreCase)
            : true;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();

var assembly = Assembly.Load("Chatter.Users.Application");
builder.Services.AddCqrs(assembly);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSharedDataAccessTypes();
builder.Services.AddUsersDataAccess(builder.Configuration);
builder.Services.AddKeycloakService();
builder.Services.AddAppLogger();
builder.AddObservability("users-api");
builder.Services.Configure<KeycloakConfig>(keycloakConfiguration);
builder.Services.AddValidatorsFromAssembly(assembly);
builder.Services.AddValidation();
builder.Services.AddCqrsDecorators();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var connectionString = config["ConnectionStrings:UsersDb"];
    var scriptPath = config["Migration:ScriptPath"];
    var absoluteScriptPath = Path.GetFullPath(scriptPath);
    var migrator = new Migrator(connectionString, absoluteScriptPath);
    await migrator.ExecutePendingMigrationsAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();