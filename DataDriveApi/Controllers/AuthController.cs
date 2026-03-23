// =====================================================================
// Controllers/AuthController.cs — equivalent of app/routers/auth.py
// =====================================================================
// Python had:
//   router = APIRouter(prefix="/auth", tags=["Authentication"])
//
//   @router.post("/signup")
//   def signup(user_data: SignupRequest): ...
//
//   @router.post("/login")
//   def login(user_data: LoginRequest): ...
//
// In C#:
//   [ApiController]        = FastAPI's automatic request validation
//   [Route("auth")]        = prefix="/auth"
//   [HttpPost("signup")]   = @router.post("/signup")
//   IActionResult          = FastAPI's return type (handles JSON serialization)
// =====================================================================

using DataDriveApi.Data;
using DataDriveApi.Models;
using DataDriveApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace DataDriveApi.Controllers;

[ApiController]       // enables automatic model validation + error responses
[Route("auth")]       // = prefix="/auth" on APIRouter
public class AuthController : ControllerBase
{
    // These are "injected" by C#'s DI system — equivalent of FastAPI's Depends()
    // You don't call "new AuthService()" yourself; C# finds and passes them for you.
    private readonly AuthService _auth;
    private readonly Database _db;

    public AuthController(AuthService auth, Database db)
    {
        _auth = auth;
        _db = db;
    }

    // ── SIGNUP ────────────────────────────────────────────────────────
    // Python:
    //   @router.post("/signup")
    //   def signup(user_data: SignupRequest):
    //       existing = get_user_from_db(user_data.email)
    //       if existing:
    //           raise HTTPException(status_code=400, detail="Email already registered")
    //       hashed = hash_password(user_data.password)
    //       ... INSERT INTO users ...
    //       return {"status": "user created"}
    [HttpPost("signup")]  // POST /auth/signup
    public IActionResult Signup(SignupRequest req)
    {
        var existing = _db.GetUserByEmail(req.Email);
        if (existing != null)
            return BadRequest(new { detail = "Email already registered" }); // = HTTPException(400, ...)

        var hashed = _auth.HashPassword(req.Password);
        try
        {
            _db.CreateUser(req.Email, hashed);
        }
        catch
        {
            return StatusCode(500, new { detail = "Database error" }); // = HTTPException(500, ...)
        }

        return Ok(new { status = "user created" }); // = return {"status": "user created"}
    }


    // ── LOGIN ─────────────────────────────────────────────────────────
    // Python:
    //   @router.post("/login")
    //   def login(user_data: LoginRequest):
    //       user = get_user_from_db(user_data.email)
    //       if not user: raise HTTPException(401, ...)
    //       if not verify_password(...): raise HTTPException(401, ...)
    //       token = create_access_token({"sub": user["email"]})
    //       return {"access_token": token, "token_type": "bearer"}
    [HttpPost("login")]   // POST /auth/login
    public IActionResult Login(LoginRequest req)
    {
        var user = _db.GetUserByEmail(req.Email);

        // Combine "user not found" + "wrong password" into same error (security best practice)
        if (user == null || !_auth.VerifyPassword(req.Password, user.HashedPassword))
            return Unauthorized(new { detail = "Invalid credentials" }); // = HTTPException(401, ...)

        var token = _auth.CreateAccessToken(user.Email);
        return Ok(new TokenResponse(token)); // = return {"access_token": token, "token_type": "bearer"}
    }
}
