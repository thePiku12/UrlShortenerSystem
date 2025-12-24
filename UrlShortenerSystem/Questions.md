# ?? Interview Questions: URL Shortener System

Grouped by topic. Use these to practice�short answers first, then deeper insights to impress.

---

## 1. Minimal Hosting & Startup

**Q1. What is `WebApplication.CreateBuilder(args)` doing?**  
- **Basic:** Sets up hosting, configuration, logging, and DI container.  
- **Deeper:** Uses the generic host with defaults (Kestrel, appsettings.json, environment variables), merges `IServiceCollection` and `IConfiguration`, simplifying the old Startup pattern.

**Q2. Why use `AddControllers()` in minimal hosting?**  
- **Basic:** Enables MVC controllers instead of minimal API endpoints.  
- **Deeper:** Adds routing, model binding, filters, and JSON formatting using `System.Text.Json`; useful for richer API behaviors (versioning, filters).

---

## 2. Dependency Injection (DI) & Lifetimes

**Q3. Explain `AddSingleton<InMemoryDatabase>()` vs `AddScoped<UrlService>()`.**  
- **Basic:** Singleton: one instance for the entire app. Scoped: one per request.  
- **Deeper:** Singleton is ideal for shared, read-heavy, thread-safe resources; Scoped suits services needing request context or carrying per-request state. Be careful injecting scoped services into singletons�that�s invalid.

**Q4. What issues can arise with singleton services?**  
- **Basic:** Thread-safety and unintended state sharing.  
- **Deeper:** Memory pressure and caching stale data; must use lock-free structures (`ConcurrentDictionary`) and avoid per-request data in singletons. Also consider graceful disposal on shutdown.

**Q5. Can a singleton depend on scoped services?**  
- **Basic:** No; the container prevents it.  
- **Deeper:** It violates lifetime rules; workaround is to depend on `IServiceScopeFactory` and create an explicit scope when needed (but only when absolutely necessary).

**Q6. Why make `UrlService` scoped?**  
- **Basic:** It aligns with per-request usage.  
- **Deeper:** Scoped services avoid state bleed; they support filters/policies and allow unit-of-work per request (if later moved to EF Core).

---

## 3. Middleware Pipeline

**Q7. Why is middleware order important?**  
- **Basic:** Each component wraps the next; wrong order breaks features.  
- **Deeper:** Authentication must precede authorization; exception handling should come early; static files precede routing; understand the short-circuit behavior of middleware.

**Q8. What does `UseHttpsRedirection()` do?**  
- **Basic:** Redirects HTTP to HTTPS.  
- **Deeper:** Uses status 307/308 (method-preserving), configurable via `HttpsRedirectionOptions`; supports reverse proxies via Forwarded Headers.

**Q9. What�s the role of `UseAuthorization()` here?**  
- **Basic:** Enforces access policies.  
- **Deeper:** Without `UseAuthentication()` and an auth scheme (JWT, cookies), it�s inert; policies rely on `IAuthorizationService`. Proper placement: after routing and authentication.

---

## 4. Controllers & Routing

**Q10. What does `MapControllers()` do?**  
- **Basic:** Maps attribute-routed controller actions.  
- **Deeper:** Integrates the endpoint routing system, creates route endpoints, supports filters, conventions, API versioning, and produces metadata for Swagger.

**Q11. Controllers vs Minimal APIs�when to choose each?**  
- **Basic:** Minimal APIs are lighter; controllers are feature-rich.  
- **Deeper:** Minimal APIs excel for small services; controllers suit complex apps with validation, filters, versioning, HATEOAS. You can mix both.

---

## 5. Swagger & API Explorer

**Q12. `AddEndpointsApiExplorer()` vs `AddSwaggerGen()`�difference?**  
- **Basic:** Explorer discovers endpoints; SwaggerGen generates OpenAPI docs.  
- **Deeper:** Explorer feeds endpoint metadata; Swagger leverages attributes (like `ProducesResponseType`) and can integrate XML comments and security schemes for JWT.

**Q13. Why restrict Swagger to development?**  
- **Basic:** To avoid exposing internal API info in production.  
- **Deeper:** In prod, expose Swagger behind auth, rate limits, or private networks. Use `app.UseSwagger(c => c.RouteTemplate = "...")` and `SwaggerUIOptions` to control access.

---

## 6. Environment & Configuration

**Q14. How does `app.Environment.IsDevelopment()` work?**  
- **Basic:** Checks the `ASPNETCORE_ENVIRONMENT` variable.  
- **Deeper:** Supports Development, Staging, Production�using different `appsettings.{Environment}.json` for environment-specific config; enables developer-exception pages.

**Q15. How would you configure options for `UrlService` (e.g., base URL)?**  
- **Basic:** Use `IConfiguration` and `IOptions<T>`.  
- **Deeper:** Bind strongly-typed options (`builder.Services.Configure<UrlOptions>(config.GetSection("Url"))`), validate with `ValidateDataAnnotations()`, and prefer `IOptionsSnapshot` for per-request updates.

---

## 7. Thread-Safety & In-Memory Storage

**Q16. Is `InMemoryDatabase` safe as a singleton?**  
- **Basic:** Only if it�s thread-safe.  
- **Deeper:** Use immutable patterns or `ConcurrentDictionary`; consider reader-writer locks if needed; beware of memory growth�add eviction strategies or migrate to persistent storage.

**Q17. How would you handle concurrency for URL insertion?**  
- **Basic:** Synchronize access.  
- **Deeper:** Use atomic operations (`GetOrAdd`), short URLs collision handling, and idempotency; consider hashing with salt, or KSUID/ULID for uniqueness.

---

## 8. Authorization vs Authentication

**Q18. What�s missing if you want secured endpoints?**  
- **Basic:** `UseAuthentication()` and an auth scheme (e.g., JWT bearer).  
- **Deeper:** Add policies, roles, claims; wire OpenAPI security; protect Swagger UI; use `[Authorize]` attributes and policy-based authorization.

---

## 9. Testing & Maintainability

**Q19. How would you unit test `UrlService`?**  
- **Basic:** Mock `InMemoryDatabase` and test methods.  
- **Deeper:** Use `IHostedService` for background tasks if needed, integration tests with `WebApplicationFactory`, and dependency boundaries for future persistence (interface-based abstractions).

**Q20. How to make `InMemoryDatabase` replaceable later?**  
- **Basic:** Create an interface and register implementations.  
- **Deeper:** Depend on an abstraction (`IUrlRepository`), allow swapping to EF Core or Redis via configuration and DI; use Repository/Unit of Work patterns.

---

## 10. Performance, Observability & Resilience

**Q21. What performance improvements would you consider?**  
- **Basic:** Use caching and efficient data structures.  
- **Deeper:** Add Response Caching, `MemoryCache`, rate limiting, minimal JSON serialization settings, HTTP/2, and async APIs; benchmark with BenchmarkDotNet and measure with dotnet counters.

**Q22. How would you add observability?**  
- **Basic:** Use logging and exception handling.  
- **Deeper:** Add structured logging (Serilog), distributed tracing (OpenTelemetry), metrics (Prometheus), health checks (`AddHealthChecks`), and Problem Details for consistent error responses.

---

## 11. Deployment & Security

**Q23. What�s required to run behind a reverse proxy?**  
- **Basic:** Configure forwarded headers.  
- **Deeper:** Use `app.UseForwardedHeaders(...)`, set `KnownProxies`, ensure HTTPS scheme is honored, and harden Kestrel limits; add rate limiting and CORS restrictions.

**Q24. How to safely enable Swagger in production?**  
- **Basic:** Protect it behind auth.  
- **Deeper:** Use API keys/JWT, restrict to internal networks, split public/private OpenAPI specs, hide sensitive endpoints, and monitor access logs.

---

## 12. URL Shortener�Specific

**Q25. How would you generate short codes?**  
- **Basic:** Hash the original URL and take a substring.  
- **Deeper:** Use Base62 encoding of a monotonic ID, avoid collisions, support custom slugs, and include checks for malicious URLs or phishing.

**Q26. How do you handle redirects correctly?**  
- **Basic:** Return an HTTP redirect status with the target URL.  
- **Deeper:** Use 301 vs 302 vs 307, preserve method on 307/308, add click tracking, rate limiting, bot detection, and prevent open redirect vulnerabilities.

**Q27. How would you add analytics (click counts)?**  
- **Basic:** Store and increment counts per short code.  
- **Deeper:** Use event sourcing, async logging, write-behind cache, aggregation windows, and data privacy considerations (GDPR).

---