using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Domain.Services;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using Prometheus;
using Serilog;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// M2: Structured logging with Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {SourceContext}{NewLine}      {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/inventory-hold-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
    .CreateLogger();

builder.Host.UseSerilog();

// M7: OpenTelemetry distributed tracing + metrics
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("inventory-hold-service"))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(options =>
            {
                var endpoint = builder.Configuration["OpenTelemetry:Endpoint"];
                if (!string.IsNullOrEmpty(endpoint))
                    options.Endpoint = new Uri(endpoint);
            });
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    });

// Kestrel: M5 — 1MB max request body size + request timeout
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 1_048_576; // 1 MB
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(120);
});

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Inventory Hold API",
        Version = "v1",
        Description = "E-Commerce checkout inventory hold microservice"
    });
});

// Security: Add authentication
if (builder.Environment.IsDevelopment())
{
    // Dev mode: No auth server needed — auto-authenticate all requests for local Docker testing
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = "DevAuth";
        options.DefaultChallengeScheme = "DevAuth";
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, WebApi.DevAuthHandler>(
        "DevAuth", options => { });
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
    });
}
else
{
    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Auth:Authority"];
            options.Audience = builder.Configuration["Auth:Audience"];
        });
}

builder.Services.AddAuthorization(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        // Dev mode: Permissive fallback — [Authorize] allows all
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAssertion(_ => true)
            .Build();
    }
});

// Security: M6 — Per-IP rate limiting via partitioned rate limiter
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);
        }
        context.HttpContext.Response.Headers["X-Rate-Limit-Retry-After"] = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var ra) ? ((int)ra.TotalSeconds).ToString() : "";
        return ValueTask.CompletedTask;
    };

    // Global fixed window: 100 requests/minute
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });

    // Per-IP sliding window: 50 requests/minute per client
    options.AddSlidingWindowLimiter("sliding", limiterOptions =>
    {
        limiterOptions.PermitLimit = 50;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow = 6;
        limiterOptions.QueueLimit = 5;
    });

    // M6: Per-IP partitioned token bucket: 30 requests/minute per client IP
    options.AddPolicy("per-ip", httpContext =>
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 30,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = 30,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

// Security: M3 — Restricted CORS (specific origins, methods, headers)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? new[] { "http://localhost:3000" };
        policy.WithOrigins(origins)
              .WithMethods("GET", "POST", "DELETE")
              .WithHeaders("Content-Type", "Authorization", "Accept")
              .AllowCredentials();
    });
});

// Domain services
builder.Services.AddScoped<HoldService>();
builder.Services.AddScoped<InventoryService>();

// Infrastructure (MongoDB, Redis, RabbitMQ, Background)
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Seed inventory data on startup (race-safe: uses upsert)
using (var scope = app.Services.CreateScope())
{
    var inventoryService = scope.ServiceProvider.GetRequiredService<InventoryService>();
    await inventoryService.SeedIfEmptyAsync();
}

// Security middleware pipeline
app.UseExceptionHandler();
app.UseHsts();
app.UseHttpsRedirection();

// Swagger (dev only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// M4: Security headers including Content-Security-Policy
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'";
    await next();
});

app.UseCors("AllowFrontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Prometheus metrics endpoint
app.MapMetrics("/metrics");

// Serilog request logging
app.UseSerilogRequestLogging();

app.Run();
