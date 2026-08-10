using RevenueOperationsDashboard.Services;
using HumanNumbers;
using HumanNumbers.Currencies;
using HumanNumbers.Formatting;
using HumanNumbers.Suffixes;
using System.Globalization;

var amharicCulture = new CultureInfo("am-ET");
amharicCulture.NumberFormat.CurrencySymbol = "ብር";
CultureInfo.DefaultThreadCurrentCulture = amharicCulture;
CultureInfo.DefaultThreadCurrentUICulture = amharicCulture;

var amharicSuffixes = new MagnitudeSuffix[]
{
    new(1_000_000_000_000_000_000m, "ኩዊ"), // Quintillion
    new(1_000_000_000_000_000m, "ኳድ"),     // Quadrillion
    new(1_000_000_000_000m, "ት"),          // Trillion
    new(1_000_000_000m, "ቢ"),              // Billion
    new(1_000_000m, "ሚ"),                  // Million
    new(1_000m, "ሺ"),                      // Thousand
    new(1m, "")                            // Fallback
};

HumanNumbersConfig.Instance.AddPolicy("AmharicCurrency", new HumanNumberFormatOptions
{
    Culture = amharicCulture,
    CurrencySymbol = "ብር",
    CurrencyPosition = CurrencyPosition.BeforeWithSpace,
    DecimalPlaces = 2,
    CachedCustomSuffixes = amharicSuffixes
});

HumanNumbersConfig.Instance.AddPolicy("AmharicCount", new HumanNumberFormatOptions
{
    Culture = amharicCulture,
    DecimalPlaces = 2,
    CachedCustomSuffixes = amharicSuffixes
});

HumanNumbersConfig.Instance.AddPolicy("EnglishCurrency", new HumanNumberFormatOptions
{
    Culture = new CultureInfo("en-US"),
    CurrencySymbol = "ETB",
    CurrencyPosition = CurrencyPosition.BeforeWithSpace,
    DecimalPlaces = 2,
    CachedCustomSuffixes = StandardSuffixSets.Default
});

HumanNumbersConfig.Instance.AddPolicy("EnglishCount", new HumanNumberFormatOptions
{
    Culture = new CultureInfo("en-US"),
    DecimalPlaces = 2,
    CachedCustomSuffixes = StandardSuffixSets.Default
});

HumanNumber.Configure(config =>
{
    config.GlobalOptions.CurrencyPosition = CurrencyPosition.BeforeWithSpace;
    config.GlobalOptions.CurrencySymbol = "ብር";
    config.GlobalOptions.CachedCustomSuffixes = amharicSuffixes;
});

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add OpenApi
builder.Services.AddOpenApi();

// Add Memory Cache for Instant Response Times
builder.Services.AddMemoryCache();

// Register Dapper Repository
builder.Services.AddScoped<DashboardRepository>();

// Enable CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.MapFallbackToFile("index.html");

app.Run();
