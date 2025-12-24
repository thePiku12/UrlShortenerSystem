using System;
using UrlShortenerSystem.Data;  
using UrlShortenerSystem.Services;
using UrlShortenerSystem.Utils;

// Creates the host and service container using minimal APIs (since .NET 6+).
// Sets up hosting, configuration, logging, and DI container
var builder = WebApplication.CreateBuilder(args);  
  
// Register dependencies  
builder.Services.AddSingleton<InMemoryDatabase>();  // Singleton is ideal for shared, read-heavy, thread-safe resources
builder.Services.AddSingleton<IDGenerator>(sp =>
{
    var machineIdStr = builder.Configuration["UrlShortener:MachineId"];
    var machineId = !string.IsNullOrWhiteSpace(machineIdStr) ? machineIdStr[0] : 'a';
    return new IDGenerator(machineId);
});
builder.Services.AddScoped<UrlService>();  // carrying per-request state

builder.Services.AddControllers();  // Enables MVC controllers instead of minimal API endpoints
builder.Services.AddEndpointsApiExplorer();  // feeds endpoint metadata
builder.Services.AddSwaggerGen();  // leverages attributes (like `ProducesResponseType`) and can integrate XML comments and security schemes for JWT.

var app = builder.Build();  
  
if (app.Environment.IsDevelopment())  // Supports Development, Staging, Production—using different `appsettings.{Environment}.json` for environment-specific config; enables developer-exception pages.
{  
    app.UseSwagger();  
    app.UseSwaggerUI();  
}  
  
app.UseHttpsRedirection();  // Uses status 307/308 (method-preserving), configurable via `HttpsRedirectionOptions`; supports reverse proxies via Forwarded Headers.
app.UseAuthorization(); // Enforces authorization policies (requires authentication if used).
app.MapControllers();  // Integrates the endpoint routing system, creates route endpoints, supports filters, conventions, API versioning, and produces metadata for Swagger

app.Run();

//Execution Flow(Typical Request)

//Request hits Kestrel → UseHttpsRedirection (if HTTP).
//Routes are matched → MapControllers.
//DI creates a scoped UrlService, injects singleton InMemoryDatabase.
//Controller action runs → business logic → returns response.
//Response sent; scoped services are disposed.
