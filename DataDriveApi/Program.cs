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
builder.Services.AddSingleton(new AuthService(secretKey, expiresMin));

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
// Python: oauth2_scheme = OAuth2PasswordBearer(tokenUrl="/auth/login")
//         payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
//
// C# configures this ONCE here — then just put [Authorize] on any endpoint
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer   = false,
            ValidateAudience = false,
            ClockSkew        = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ── 5. BUILD & MIDDLEWARE PIPELINE ────────────────────────────────────
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
