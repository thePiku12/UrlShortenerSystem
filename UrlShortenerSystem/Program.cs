using System; // Used for basic .NET types (e.g., string, DateTime)
using UrlShortenerSystem.Data;  // Brings InMemoryDatabase into scope for DI registration
using UrlShortenerSystem.Services; // Brings UrlService into scope for DI registration
using UrlShortenerSystem.Utils; // Brings IDGenerator into scope for DI registration

// WebApplication.CreateBuilder bootstraps hosting, config, logging, and Dependency Injection (DI) container.
var builder = WebApplication.CreateBuilder(args);

// DI registration: Singleton means one instance for the entire app lifetime (per process).
// Interview: why singleton? Shared in-memory state should be consistent across requests,
// and ConcurrentDictionary is thread-safe for concurrent reads/writes inside a process.
builder.Services.AddSingleton<InMemoryDatabase>();

// MachineId must be exactly 2 chars: A0..A9, B0..B9, ... Z0..Z9
// Configure via appsettings.json/env var: MachineId=A0 (default A0 if missing)
var machineId = builder.Configuration["MachineId"] ?? "A0";
builder.Services.AddSingleton(new IDGenerator(machineId));

// Scoped means one instance per request.
// Interview: why scoped? UrlService can be created per request; it holds no global state,
// and it depends on singletons for shared storage and ID generation.
builder.Services.AddScoped<UrlService>();  // carrying per-request state

// Adds MVC controller support (attribute routing, model binding, validation, etc.)
builder.Services.AddControllers();  // Enables MVC controllers instead of minimal API endpoints

// Enables endpoint metadata for Swagger/OpenAPI.
builder.Services.AddEndpointsApiExplorer();  // feeds endpoint metadata

// Registers Swagger generator.
builder.Services.AddSwaggerGen();  // leverages attributes (like `ProducesResponseType`) and can integrate XML comments and security schemes for JWT.

var app = builder.Build();

if (app.Environment.IsDevelopment())  // Supports Development, Staging, Production—using different `appsettings.{Environment}.json` for environment-specific config; enables developer-exception pages.
{
    // Development-only: expose Swagger docs + UI.
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Middleware that redirects HTTP -> HTTPS.
// Interview: runs early so downstream sees secure scheme; helps security defaults.
app.UseHttpsRedirection();  // Uses status 307/308 (method-preserving), configurable via `HttpsRedirectionOptions`; supports reverse proxies via Forwarded Headers.

// Authorization middleware.
// Interview: does nothing unless authentication/authorization policies are configured,
// but keeps pipeline ready for future auth.
app.UseAuthorization(); // Enforces authorization policies (requires authentication if used).

// Maps controller routes into endpoint routing system.
app.MapControllers();  // Integrates the endpoint routing system, creates route endpoints, supports filters, conventions, API versioning, and produces metadata for Swagger


app.Run();

//Execution Flow(Typical Request)

//Request hits Kestrel → UseHttpsRedirection (if HTTP).
//Routes are matched → MapControllers.
//DI creates a scoped UrlService, injects singleton InMemoryDatabase.
//Controller action runs → business logic → returns response.
//Response sent; scoped services are disposed.
