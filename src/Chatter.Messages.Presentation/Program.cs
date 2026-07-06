using System.Reflection;
using Chatter.MessagesDataAccess;
using Core.Migrator;
using Core.CQRS;
using Core.CQRS.Decorators;
using Core.Validation;
using FluentValidation;
using Core.DataAccessTypes;
using Core.Infrastructure;
using Core.Logger;
using Core.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var keycloakConfiguration = builder.Configuration.GetSection("Keycloak");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakConfiguration["Authority"];
        options.Audience = keycloakConfiguration["Audience"];
        
        options.RequireHttpsMetadata = keycloakConfiguration["RequireHttpsMetadata"] != null ?
            keycloakConfiguration["RequireHttpsMetadata"].Equals("true", StringComparison.OrdinalIgnoreCase) :
            true;
        
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            // ValidateIssuer = true,
            // ValidateLifetime = true,
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.AddObservability("messages-api");
builder.Services.AddHttpContextAccessor();
builder.Services.AddLogging();
var assembly = Assembly.Load("Chatter.Messages.Application");
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSharedDataAccessTypes();
builder.Services.AddMessagesDataAccess(builder.Configuration);

builder.Services.AddAppLogger();
builder.Services.AddCqrs(assembly);
builder.Services.AddValidatorsFromAssembly(assembly);
builder.Services.AddValidation();
builder.Services.AddCqrsDecorators();
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var connectionString = config["ConnectionStrings:MessagesDb"];
    var scriptPath = config["Migration:ScriptPath"];
    var absoluteScriptPath = Path.GetFullPath(scriptPath);
    var migrator = new Migrator(connectionString, absoluteScriptPath); // scriptPath optional
    await migrator.ExecutePendingMigrationsAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();