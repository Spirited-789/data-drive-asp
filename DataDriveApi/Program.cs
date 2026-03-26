// =====================================================================
// Program.cs  — equivalent of app/main.py (FULL VERSION with DI wiring)
// =====================================================================

using System.Text;
using DataDriveApi.Data;
using DataDriveApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── 1. READ CONFIG ────────────────────────────────────────────────────
// Python equivalent: load_dotenv(); SECRET_KEY = os.getenv("SECRET_KEY")
var secretKey   = builder.Configuration["Jwt:SecretKey"]    ?? throw new Exception("Jwt:SecretKey not set");
var dbConn      = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new Exception("DB connection not set");
var expiresMin  = builder.Configuration.GetValue<int>("Jwt:ExpiresMinutes", 30);
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
var azureClientId = builder.Configuration["AzureAd:ClientId"];
var azureTenantId = builder.Configuration["AzureAd:TenantId"];

// ── 2. REGISTER SERVICES (Dependency Injection) ───────────────────────
// This is C#'s equivalent of FastAPI's Depends() system.
// You register services ONCE here, and C# automatically injects them
// into any Controller that asks for them in its constructor.
//
// Python had:          def signup(user_data, db=Depends(get_db), auth=Depends(get_auth))
// C# equivalent:       public AuthController(Database db, AuthService auth)  ← auto-injected

builder.Services.AddControllers();

// Register Database — "Singleton" means one instance shared for the app lifetime
// Python equivalent: there was no DI; you called get_conn() directly each time
builder.Services.AddSingleton(new Database(dbConn));

// Register AuthService
builder.Services.AddSingleton(new AuthService(secretKey, expiresMin, azureClientId, azureTenantId));

// Register HttpClient factory (used by DataController for requests.get() equivalent)
builder.Services.AddHttpClient();

// ── 3. CORS ───────────────────────────────────────────────────────────
// Python: app.add_middleware(CORSMiddleware, allow_origins=ALLOWED_ORIGINS, ...)
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials()));

// ── 4. JWT AUTHENTICATION ─────────────────────────────────────────────
// Supports BOTH:
//   1. Local tokens (from AuthController login)
//   2. Microsoft tokens (from Entra ID login)

builder.Services.AddAuthentication(opts =>
    {
        opts.DefaultAuthenticateScheme = "Local";
        opts.DefaultChallengeScheme = "Local";
    })
    .AddJwtBearer("Local", opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    })
    .AddJwtBearer("EntraID", opts =>
    {
        // AUTHORITY: e.g. https://login.microsoftonline.com/9188.../v2.0
        opts.Authority = $"https://login.microsoftonline.com/{azureTenantId}/v2.0";
        opts.Audience = azureClientId;
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidAudience = azureClientId,
            // Skip signing key validation if we want to be "loose" like the Python PoC,
            // but setting Authority automatically handles JWKS key rotation for us!
            ValidateIssuerSigningKey = true, 
            ClockSkew = TimeSpan.Zero
        };
    });

// Define a policy that allows EITHER local OR Microsoft tokens
builder.Services.AddAuthorization(opts =>
{
    var defaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder("Local", "EntraID")
        .RequireAuthenticatedUser()
        .Build();
    opts.DefaultPolicy = defaultPolicy;
});

// ── 5. BIND TO PORT FOR RENDER ─────────────────────────────────────────
// Render dynamically assigns a port to the container via $PORT.
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://*:{port}");

// ── 6. BUILD & MIDDLEWARE PIPELINE ────────────────────────────────────
var app = builder.Build();

app.UseCors();
app.UseAuthentication();   // validates JWT — runs before every request
app.UseAuthorization();    // enforces [Authorize] attributes

app.MapControllers();      // auto-registers all Controller routes

// Health check — Python: @app.get("/") def root(): return {"status": "..."}
app.MapGet("/", () => new { status = "C# DataDriveApi backend running" });

// ── 6. INIT DB ON STARTUP ─────────────────────────────────────────────
// Python: init_db_users(); init_db()  (called at module load in main.py)
var db = app.Services.GetRequiredService<Database>();
db.InitDbUsers();
db.InitDb();

// ── 7. RUN ────────────────────────────────────────────────────────────
// Python: uvicorn main:app --reload
app.Run();
