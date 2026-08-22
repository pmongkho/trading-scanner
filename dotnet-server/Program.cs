using System.Text;
using TradingScanner._Data;
using TradingScanner._Models.Entities;
using TradingScanner._Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using TradingScanner.Application.Configuration;
using TradingScanner.Application.Interfaces;
using TradingScanner.Application.Services;
using TradingScanner.Hubs;
using TradingScanner.Infrastructure.MarketData;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOptions<ScannerSettings>()
    .Bind(builder.Configuration.GetSection(ScannerSettings.SectionName))
    .Validate(x => x.MinimumPrice > 0 && x.MaximumPrice > x.MinimumPrice, "Scanner price range is invalid.")
    .Validate(x => x.MinimumGapPercent >= 0 && x.MinimumVolume >= 0 && x.MinimumRelativeVolume >= 0,
        "Scanner thresholds cannot be negative.")
    .Validate(x => x.MaximumFloat > 0 && x.PreferredFloat > 0 && x.PreferredFloat <= x.MaximumFloat,
        "Scanner float limits are invalid.")
    .Validate(x => x.FrontendUpdateMilliseconds > 0 && x.AlertCooldownSeconds >= 0,
        "Scanner timing values are invalid.")
    .Validate(x => x.ScoreWeights.Total == 100, "Scanner score weights must total 100.")
    .Validate(x => x.Momentum.BuildingScore <= x.Momentum.AcceleratingScore
        && x.Momentum.AcceleratingScore <= x.Momentum.StrongScore,
        "Momentum thresholds must be in ascending order.")
    .Validate(x => x.MarketStream.Symbols.Length > 0 && x.MarketStream.SyntheticIntervalMilliseconds > 0,
        "Market stream symbols and interval are invalid.")
    .Validate(x => x.MarketStream.Provider.Equals("Synthetic", StringComparison.OrdinalIgnoreCase)
        || x.MarketStream.Provider.Equals("Alpaca", StringComparison.OrdinalIgnoreCase),
        "Market stream provider must be Synthetic or Alpaca.")
    .Validate(x => x.News.PollSeconds > 0 && x.News.LookbackMinutes > 0,
        "News polling values must be positive.")
    .ValidateOnStart();
builder.Services.AddSingleton<IMarketSessionService, MarketSessionService>();
builder.Services.AddSingleton<IRelativeVolumeService, RollingRelativeVolumeService>();
builder.Services.AddSingleton<IIndicatorEngine, IndicatorEngine>();
builder.Services.AddSingleton<IMomentumEngine, MomentumEngine>();
builder.Services.AddSingleton<IAPlusScoringEngine, APlusScoringEngine>();
builder.Services.AddSingleton<IFilteredViewService, FilteredViewService>();
builder.Services.AddSingleton<IMarketHeatService, MarketHeatService>();
builder.Services.AddSingleton<ICatalystClassifier, CatalystClassifier>();
builder.Services.AddSingleton<ITickerStateManager, TickerStateManager>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IMarketDataProvider>(services =>
{
    var scanner = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ScannerSettings>>().Value;
    return scanner.MarketStream.Provider.Equals("Alpaca", StringComparison.OrdinalIgnoreCase)
        ? ActivatorUtilities.CreateInstance<AlpacaMarketDataProvider>(services)
        : ActivatorUtilities.CreateInstance<SyntheticMarketDataProvider>(services);
});
builder.Services.AddHostedService<MarketStreamService>();
builder.Services.AddHostedService<ScannerSnapshotPublisher>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<AlpacaNewsService>();

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)));

builder.Services
    .AddIdentityCore<User>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole<int>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

builder.Services.AddScoped<AuthService>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is missing.");

var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? ["http://localhost:4200"];

        policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<MarketHub>("/hubs/market");

app.Run();
