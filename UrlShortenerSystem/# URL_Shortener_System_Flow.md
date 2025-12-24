# URL Shortener System — End-to-End Flow (Code Walkthrough)

This document explains how the current URL Shortener implementation works end-to-end, based on the files in the solution.

---

## 1) High-level architecture (what exists in code)

The runtime is an ASP.NET Core Web API application with:

- **Controller layer**: `UrlController`
- **Service layer**: `UrlService` (business logic)
- **Data layer (in-memory storage)**: `InMemoryDatabase`
- **ID generation utilities**:
  - `IDGenerator` (machineId + sequence)
  - `Base62Encoder` (used by `IDGenerator`, must exist in `Utils/`)

Key characteristics of the current implementation:

- **Single-process persistence**: data is stored in memory; restarting the app loses mappings and counters.
- **Unique short code generation**: based on an in-process sequence per machine ID (not durable across restarts).
- **Redirect + optional "noRedirect" return**: supports either HTTP redirect or returning JSON.
- **Basic analytics**: increments `HitCount` atomically using `Interlocked`.

---

## 2) Startup & Dependency Injection flow (`Program.cs`)

### 2.1 App bootstrap
The app starts with minimal hosting:

- `var builder = WebApplication.CreateBuilder(args);`
- Services are registered into ASP.NET Core DI container.
- `var app = builder.Build();`
- Middleware and endpoints are configured.
- `app.Run();` starts Kestrel web server.

### 2.2 Dependencies registered

#### InMemoryDatabase (Singleton)
```csharp
builder.Services.AddSingleton<InMemoryDatabase>();
```

- **Singleton** means one instance for the entire process lifetime.
- All requests share the same `_store` and `_urlToCode` dictionaries.
- This enables shared state (short URL mappings + counters) inside one running server.

#### IDGenerator (Singleton)
```csharp
builder.Services.AddSingleton<IDGenerator>(sp =>
{
    var machineIdStr = builder.Configuration["UrlShortener:MachineId"];
    var machineId = !string.IsNullOrWhiteSpace(machineIdStr) ? machineIdStr[0] : 'a';
    return new IDGenerator(machineId);
});
```

- Also a singleton, so its `_sequence` is shared across requests in this process.
- Reads `UrlShortener:MachineId` from configuration; defaults to `'a'` if missing.
- MachineId exists so multiple instances *can* use different prefixes (but only if configured correctly).

#### UrlService (Scoped)
```csharp
builder.Services.AddScoped<UrlService>();
```

- `Scoped` means *one instance per HTTP request*.
- A new `UrlService` is created each request but it uses the same `InMemoryDatabase` and `IDGenerator` singletons.

### 2.3 Middleware pipeline
In development:
- Swagger UI enabled.

Always:
- `UseHttpsRedirection()`
- `UseAuthorization()` (no auth configured, but middleware is set)
- `MapControllers()` maps routes from controller attributes.

---

## 3) API surface (Controller routes)

Defined in `Controllers/UrlController.cs`:

Base route: `[Route("url")]`

### 3.1 Shorten URL
**POST** `/url/shorten`

Request body (`ShortenRequest`):
```json
{
  "originalUrl": "http://example.com"
}
```

Response (`ShortenResponse`):
```json
{
  "shortCode": "a0000001",
  "shortUrl": "https://localhost:5001/url/a0000001",
  "expiryDate": "2030-12-24T00:00:00Z"
}
```

### 3.2 Redirect to original
**GET** `/url/{shortCode}`

Optional query param:
- `?noRedirect=true` returns JSON instead of redirect.

Behavior:
- If found and not expired: redirect (HTTP 302) to original URL
- If expired/not found: returns HTTP 410 with message `"Link expired or not found."`

### 3.3 Get stats
**GET** `/url/{shortCode}/stats`

Returns:
- shortCode, originalUrl, createdAt, expiryDate, hitCount, isExpired
- 404 if shortCode not found

---

## 4) Core shortening flow (POST /url/shorten)

### 4.1 Request enters controller
`UrlController.Shorten([FromBody] ShortenRequest request)`

Steps:
1. Validates non-empty `request.OriginalUrl`.
2. Builds a baseDomain (host + scheme):
   - `var baseDomain = $"{Request.Scheme}://{Request.Host}";`
3. Calls service:
   - `_service.Shorten(request.OriginalUrl, baseDomain)`
4. Returns `200 OK` with `ShortenResponse`.

### 4.2 Business logic in `UrlService.Shorten`
File: `Services/UrlService.cs`

The main steps:

#### Step A — Idempotency (optional)
```csharp
var existingCode = _db.GetShortCodeByUrl(originalUrl);
```
- Looks up if this original URL was previously shortened (via `_urlToCode` index).
- If found, retrieves the record and returns existing short URL without creating new entry.

This means: **same long URL tends to return same short code** (as long as the service instance hasn’t restarted).

#### Step B — Generate a short code
```csharp
var code = _idGenerator.GenerateCode(totalLength: 8);
```

- `totalLength: 8` yields an 8-character code:
  - 1 character machineId + 7 character base62 sequence, left padded with `'0'`.

Example:
- machineId = `'a'`
- sequence = 1
- base62 = `"b"` (depending on alphabet order in Base62Encoder)
- padded to 7: `"000000b"`
- final shortCode: `"a000000b"`

> Note: exact output depends on the Base62 alphabet used in `Base62Encoder`.

#### Step C — Create the record
```csharp
record = new UrlRecord
{
    ShortCode = code,
    OriginalUrl = originalUrl,
    CreatedAt = DateTime.UtcNow,
    ExpiryDate = DateTime.UtcNow.AddYears(5)
};
```
- The code stores a 5-year expiration timestamp by default.

#### Step D — Store it safely (avoid overwrites)
```csharp
if (_db.TryAdd(record))
{
    _db.UpsertUrlIndex(originalUrl, code);
    return new ShortenResponse { ... };
}
```

`TryAdd` ensures:
- if the same `ShortCode` already exists, it will not overwrite it.
- instead it returns `false` and the service retries.

#### Step E — Collision retry loop
```csharp
const int maxAttempts = 10;
for (var attempt = 0; attempt < maxAttempts; attempt++)
{
   ...
}
throw new InvalidOperationException(...);
```
- Retries up to 10 times if a generated code already exists.
- In this design collisions should only happen because of:
  - code already in store (extremely unlikely unless sequence resets or machineId duplicates)
  - concurrent/racing scenarios across processes (not handled by in-memory DB)

### 4.3 Output
The service returns `ShortenResponse`:
- `ShortCode`: the generated code
- `ShortUrl`: `${baseDomain}/url/{code}`
- `ExpiryDate`: from the record

Controller returns that as JSON.

---

## 5) Redirection flow (GET /url/{shortCode})

### 5.1 Request enters controller
`UrlController.RedirectToOriginal(string shortCode, bool noRedirect=false)`

Steps:
1. Calls:
   - `var originalUrl = _service.Resolve(shortCode);`
2. If `originalUrl == null`:
   - returns HTTP 410 Gone
3. If `noRedirect == true`:
   - returns JSON `{ OriginalUrl = originalUrl }`
4. Else:
   - `return Redirect(originalUrl);` (302 redirect)

### 5.2 Service logic (`UrlService.Resolve`)
Steps:
1. `var record = _db.Get(shortCode);`
2. If missing: return null.
3. If expired: return null.
4. Increment analytics:
   - `_db.IncrementHitCount(shortCode, out _);`
5. Return `record.OriginalUrl`.

### 5.3 Analytics increment (`InMemoryDatabase.IncrementHitCount`)
```csharp
Interlocked.Increment(ref record.HitCountRef);
```

- Uses `Interlocked.Increment` to ensure increments are atomic and safe under concurrency.
- This requires `UrlRecord` to expose a ref-returning member `HitCountRef`.

---

## 6) Stats flow (GET /url/{shortCode}/stats)

### 6.1 Controller
`UrlController.GetStats(string shortCode)`

Steps:
1. Calls service:
   - `_service.GetStats(shortCode)`
2. If null: returns 404 Not Found.
3. Else returns 200 OK with a stats object.

### 6.2 Service logic (`UrlService.GetStats`)
Steps:
1. Reads record from DB:
   - `_db.Get(shortCode)`
2. If not found: return null.
3. Returns `StatsResponse`:
   - `HitCount` comes from `record.HitCount`
   - `IsExpired` computed from current time vs `ExpiryDate`

---

## 7) Data storage model (`InMemoryDatabase`)

### 7.1 Structures
```csharp
ConcurrentDictionary<string, UrlRecord> _store;
ConcurrentDictionary<string, string> _urlToCode;
```

- `_store` maps `shortCode -> UrlRecord`
- `_urlToCode` maps `originalUrl -> shortCode` (supports idempotent shortening)

### 7.2 Thread safety
- `ConcurrentDictionary` is thread-safe for reads/writes.
- `Interlocked` makes `HitCount` increments atomic.

Important: although this is thread-safe **within one process**, it is **not distributed**.

---

## 8) ID generation details (`IDGenerator`)

### 8.1 Inputs
- `machineId`: one character (letter/digit).
- `totalLength`: defaults to 8.

### 8.2 Output shape
- Code = `machineId + base62(sequence).PadLeft(totalLength - 1, '0')`

### 8.3 Concurrency control
```csharp
lock (_lock)
{
    _sequence++;
    ...
}
```
- Guarantees that within this process, two requests will not obtain same sequence number.

### 8.4 Limitations (important)
- Sequence resets when process restarts.
- If multiple app instances run with same machineId, they may generate duplicates.
- Fixed length guarantee breaks once sequence grows too large to fit in `totalLength-1` base62 digits.

---

## 9) Expiration behavior

### 9.1 Where expiration is enforced
In `UrlService.Resolve`:
- If `DateTime.UtcNow > record.ExpiryDate`, returns null.
- Controller maps null to HTTP 410 Gone.

Stats endpoint still returns stats even if expired (it includes `IsExpired`).

---

## 10) Summary of the end-to-end lifecycle

### A) Shorten
1. Client calls `POST /url/shorten` with `originalUrl`.
2. Controller validates input.
3. Service checks if URL already shortened (idempotency).
4. If not: generator creates a new short code.
5. Record is stored in `_store`, URL index stored in `_urlToCode`.
6. Response returns shortUrl and expiryDate.

### B) Redirect
1. Client calls `GET /url/{shortCode}`.
2. Service looks up record, validates expiry.
3. Atomic `HitCount` increment occurs.
4. Controller redirects (302) to the original URL.

### C) Stats
1. Client calls `GET /url/{shortCode}/stats`.
2. Service fetches record and returns hit count and expiration state.

---

## 11) Production gaps (what this code does not cover)
This section is informational; it highlights what would be required beyond in-memory implementation:

- **Durability**: data is lost on restart.
- **Distributed uniqueness**: multiple app instances need shared storage and unique enforcement.
- **Persisted counters**: hit counts should survive restarts.
- **Caching**: no Redis/memory cache tier for hot keys.
- **Abuse prevention**: no URL validation/safelisting/blacklisting/rate limiting.

---
```# URL Shortener System — End-to-End Flow (Code Walkthrough)

This document explains how the current URL Shortener implementation works end-to-end, based on the files in the solution.

---

## 1) High-level architecture (what exists in code)

The runtime is an ASP.NET Core Web API application with:

- **Controller layer**: `UrlController`
- **Service layer**: `UrlService` (business logic)
- **Data layer (in-memory storage)**: `InMemoryDatabase`
- **ID generation utilities**:
  - `IDGenerator` (machineId + sequence)
  - `Base62Encoder` (used by `IDGenerator`, must exist in `Utils/`)

Key characteristics of the current implementation:

- **Single-process persistence**: data is stored in memory; restarting the app loses mappings and counters.
- **Unique short code generation**: based on an in-process sequence per machine ID (not durable across restarts).
- **Redirect + optional "noRedirect" return**: supports either HTTP redirect or returning JSON.
- **Basic analytics**: increments `HitCount` atomically using `Interlocked`.

---

## 2) Startup & Dependency Injection flow (`Program.cs`)

### 2.1 App bootstrap
The app starts with minimal hosting:

- `var builder = WebApplication.CreateBuilder(args);`
- Services are registered into ASP.NET Core DI container.
- `var app = builder.Build();`
- Middleware and endpoints are configured.
- `app.Run();` starts Kestrel web server.

### 2.2 Dependencies registered

#### InMemoryDatabase (Singleton)
```csharp
builder.Services.AddSingleton<InMemoryDatabase>();
```

- **Singleton** means one instance for the entire process lifetime.
- All requests share the same `_store` and `_urlToCode` dictionaries.
- This enables shared state (short URL mappings + counters) inside one running server.

#### IDGenerator (Singleton)
```csharp
builder.Services.AddSingleton<IDGenerator>(sp =>
{
    var machineIdStr = builder.Configuration["UrlShortener:MachineId"];
    var machineId = !string.IsNullOrWhiteSpace(machineIdStr) ? machineIdStr[0] : 'a';
    return new IDGenerator(machineId);
});
```

- Also a singleton, so its `_sequence` is shared across requests in this process.
- Reads `UrlShortener:MachineId` from configuration; defaults to `'a'` if missing.
- MachineId exists so multiple instances *can* use different prefixes (but only if configured correctly).

#### UrlService (Scoped)
```csharp
builder.Services.AddScoped<UrlService>();
```

- `Scoped` means *one instance per HTTP request*.
- A new `UrlService` is created each request but it uses the same `InMemoryDatabase` and `IDGenerator` singletons.

### 2.3 Middleware pipeline
In development:
- Swagger UI enabled.

Always:
- `UseHttpsRedirection()`
- `UseAuthorization()` (no auth configured, but middleware is set)
- `MapControllers()` maps routes from controller attributes.

---

## 3) API surface (Controller routes)

Defined in `Controllers/UrlController.cs`:

Base route: `[Route("url")]`

### 3.1 Shorten URL
**POST** `/url/shorten`

Request body (`ShortenRequest`):
```json
{
  "originalUrl": "http://example.com"
}
```

Response (`ShortenResponse`):
```json
{
  "shortCode": "a0000001",
  "shortUrl": "https://localhost:5001/url/a0000001",
  "expiryDate": "2030-12-24T00:00:00Z"
}
```

### 3.2 Redirect to original
**GET** `/url/{shortCode}`

Optional query param:
- `?noRedirect=true` returns JSON instead of redirect.

Behavior:
- If found and not expired: redirect (HTTP 302) to original URL
- If expired/not found: returns HTTP 410 with message `"Link expired or not found."`

### 3.3 Get stats
**GET** `/url/{shortCode}/stats`

Returns:
- shortCode, originalUrl, createdAt, expiryDate, hitCount, isExpired
- 404 if shortCode not found

---

## 4) Core shortening flow (POST /url/shorten)

### 4.1 Request enters controller
`UrlController.Shorten([FromBody] ShortenRequest request)`

Steps:
1. Validates non-empty `request.OriginalUrl`.
2. Builds a baseDomain (host + scheme):
   - `var baseDomain = $"{Request.Scheme}://{Request.Host}";`
3. Calls service:
   - `_service.Shorten(request.OriginalUrl, baseDomain)`
4. Returns `200 OK` with `ShortenResponse`.

### 4.2 Business logic in `UrlService.Shorten`
File: `Services/UrlService.cs`

The main steps:

#### Step A — Idempotency (optional)
```csharp
var existingCode = _db.GetShortCodeByUrl(originalUrl);
```
- Looks up if this original URL was previously shortened (via `_urlToCode` index).
- If found, retrieves the record and returns existing short URL without creating new entry.

This means: **same long URL tends to return same short code** (as long as the service instance hasn’t restarted).

#### Step B — Generate a short code
```csharp
var code = _idGenerator.GenerateCode(totalLength: 8);
```

- `totalLength: 8` yields an 8-character code:
  - 1 character machineId + 7 character base62 sequence, left padded with `'0'`.

Example:
- machineId = `'a'`
- sequence = 1
- base62 = `"b"` (depending on alphabet order in Base62Encoder)
- padded to 7: `"000000b"`
- final shortCode: `"a000000b"`

> Note: exact output depends on the Base62 alphabet used in `Base62Encoder`.

#### Step C — Create the record
```csharp
record = new UrlRecord
{
    ShortCode = code,
    OriginalUrl = originalUrl,
    CreatedAt = DateTime.UtcNow,
    ExpiryDate = DateTime.UtcNow.AddYears(5)
};
```
- The code stores a 5-year expiration timestamp by default.

#### Step D — Store it safely (avoid overwrites)
```csharp
if (_db.TryAdd(record))
{
    _db.UpsertUrlIndex(originalUrl, code);
    return new ShortenResponse { ... };
}
```

`TryAdd` ensures:
- if the same `ShortCode` already exists, it will not overwrite it.
- instead it returns `false` and the service retries.

#### Step E — Collision retry loop
```csharp
const int maxAttempts = 10;
for (var attempt = 0; attempt < maxAttempts; attempt++)
{
   ...
}
throw new InvalidOperationException(...);
```
- Retries up to 10 times if a generated code already exists.
- In this design collisions should only happen because of:
  - code already in store (extremely unlikely unless sequence resets or machineId duplicates)
  - concurrent/racing scenarios across processes (not handled by in-memory DB)

### 4.3 Output
The service returns `ShortenResponse`:
- `ShortCode`: the generated code
- `ShortUrl`: `${baseDomain}/url/{code}`
- `ExpiryDate`: from the record

Controller returns that as JSON.

---

## 5) Redirection flow (GET /url/{shortCode})

### 5.1 Request enters controller
`UrlController.RedirectToOriginal(string shortCode, bool noRedirect=false)`

Steps:
1. Calls:
   - `var originalUrl = _service.Resolve(shortCode);`
2. If `originalUrl == null`:
   - returns HTTP 410 Gone
3. If `noRedirect == true`:
   - returns JSON `{ OriginalUrl = originalUrl }`
4. Else:
   - `return Redirect(originalUrl);` (302 redirect)

### 5.2 Service logic (`UrlService.Resolve`)
Steps:
1. `var record = _db.Get(shortCode);`
2. If missing: return null.
3. If expired: return null.
4. Increment analytics:
   - `_db.IncrementHitCount(shortCode, out _);`
5. Return `record.OriginalUrl`.

### 5.3 Analytics increment (`InMemoryDatabase.IncrementHitCount`)
```csharp
Interlocked.Increment(ref record.HitCountRef);
```

- Uses `Interlocked.Increment` to ensure increments are atomic and safe under concurrency.
- This requires `UrlRecord` to expose a ref-returning member `HitCountRef`.

---

## 6) Stats flow (GET /url/{shortCode}/stats)

### 6.1 Controller
`UrlController.GetStats(string shortCode)`

Steps:
1. Calls service:
   - `_service.GetStats(shortCode)`
2. If null: returns 404 Not Found.
3. Else returns 200 OK with a stats object.

### 6.2 Service logic (`UrlService.GetStats`)
Steps:
1. Reads record from DB:
   - `_db.Get(shortCode)`
2. If not found: return null.
3. Returns `StatsResponse`:
   - `HitCount` comes from `record.HitCount`
   - `IsExpired` computed from current time vs `ExpiryDate`

---

## 7) Data storage model (`InMemoryDatabase`)

### 7.1 Structures
```csharp
ConcurrentDictionary<string, UrlRecord> _store;
ConcurrentDictionary<string, string> _urlToCode;
```

- `_store` maps `shortCode -> UrlRecord`
- `_urlToCode` maps `originalUrl -> shortCode` (supports idempotent shortening)

### 7.2 Thread safety
- `ConcurrentDictionary` is thread-safe for reads/writes.
- `Interlocked` makes `HitCount` increments atomic.

Important: although this is thread-safe **within one process**, it is **not distributed**.

---

## 8) ID generation details (`IDGenerator`)

### 8.1 Inputs
- `machineId`: one character (letter/digit).
- `totalLength`: defaults to 8.

### 8.2 Output shape
- Code = `machineId + base62(sequence).PadLeft(totalLength - 1, '0')`

### 8.3 Concurrency control
```csharp
lock (_lock)
{
    _sequence++;
    ...
}
```
- Guarantees that within this process, two requests will not obtain same sequence number.

### 8.4 Limitations (important)
- Sequence resets when process restarts.
- If multiple app instances run with same machineId, they may generate duplicates.
- Fixed length guarantee breaks once sequence grows too large to fit in `totalLength-1` base62 digits.

---

## 9) Expiration behavior

### 9.1 Where expiration is enforced
In `UrlService.Resolve`:
- If `DateTime.UtcNow > record.ExpiryDate`, returns null.
- Controller maps null to HTTP 410 Gone.

Stats endpoint still returns stats even if expired (it includes `IsExpired`).

---

## 10) Summary of the end-to-end lifecycle

### A) Shorten
1. Client calls `POST /url/shorten` with `originalUrl`.
2. Controller validates input.
3. Service checks if URL already shortened (idempotency).
4. If not: generator creates a new short code.
5. Record is stored in `_store`, URL index stored in `_urlToCode`.
6. Response returns shortUrl and expiryDate.

### B) Redirect
1. Client calls `GET /url/{shortCode}`.
2. Service looks up record, validates expiry.
3. Atomic `HitCount` increment occurs.
4. Controller redirects (302) to the original URL.

### C) Stats
1. Client calls `GET /url/{shortCode}/stats`.
2. Service fetches record and returns hit count and expiration state.

---

## 11) Production gaps (what this code does not cover)
This section is informational; it highlights what would be required beyond in-memory implementation:

- **Durability**: data is lost on restart.
- **Distributed uniqueness**: multiple app instances need shared storage and unique enforcement.
- **Persisted counters**: hit counts should survive restarts.
- **Caching**: no Redis/memory cache tier for hot keys.
- **Abuse prevention**: no URL validation/safelisting/blacklisting/rate limiting.

---