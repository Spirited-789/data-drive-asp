// =====================================================================
// Models/Dtos.cs — equivalent of app/models/schemas.py
// =====================================================================
// In Python (Pydantic):
//   class LoginRequest(BaseModel):
//       email: str
//       password: str
//
// In C#, a "record" does the same thing:
//   - Automatically validates that fields exist in the JSON
//   - Auto-generates constructor, equality checks, and ToString()
//   - No library needed — it's built into the language
//
// "record" = Pydantic BaseModel, but built into C# itself.
// =====================================================================

namespace DataDriveApi.Models;

// ── AUTH MODELS ──────────────────────────────────────────────────────

// Python: class LoginRequest(BaseModel): email: str; password: str
public record LoginRequest(string Email, string Password);

// Python: class SignupRequest(BaseModel): email: str; password: str
public record SignupRequest(string Email, string Password);

// Python: class TokenResponse(BaseModel): access_token: str; token_type: str = "bearer"
public record TokenResponse(string AccessToken, string TokenType = "bearer");


// ── DATA MODELS ───────────────────────────────────────────────────────

// Python: class IngestRequest(BaseModel): url: str
public record IngestRequest(string Url);

// Python: class IngestResponse(BaseModel): status: str; records_ingested: int; timestamp: str
public record IngestResponse(string Status, int RecordsIngested, string Timestamp);


// ── DATABASE ENTITY ───────────────────────────────────────────────────
// This represents a row from the "users" table (not in your Python code
// explicitly, but implied by your dict access: user["hashed_password"])

public record UserRecord(int Id, string Email, string HashedPassword, string CreatedAt);
