// =====================================================================
// Services/AuthService.cs — equivalent of app/services/auth.py
// =====================================================================
// Python had:
//   pwd_context = CryptContext(schemes=["bcrypt"])
//   def hash_password(p): return pwd_context.hash(p)
//   def verify_password(p, h): return pwd_context.verify(p, h)
//   def create_access_token(data): return jwt.encode(...)
//
// C# equivalent uses:
//   BCrypt.Net — same bcrypt algorithm, same hashes work cross-language
//   System.IdentityModel.Tokens.Jwt — built-in JWT library
// =====================================================================

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace DataDriveApi.Services;

public class AuthService
{
    private readonly string _secretKey;
    private readonly int _expiresMinutes;

    // Constructor = __init__ in Python.
    // "string secretKey" = typed parameter (no "str secretKey" like Python would have)
    public AuthService(string secretKey, int expiresMinutes = 30)
    {
        _secretKey = secretKey;
        _expiresMinutes = expiresMinutes;
    }

    // ── PASSWORD HASHING ─────────────────────────────────────────────
    // Python: def hash_password(password: str) -> str:
    //             return pwd_context.hash(password)
    public string HashPassword(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password);
    //  ↑ "=>" on one line = shorthand for { return ...; }
    //    same as Python's single-line lambda body

    // Python: def verify_password(plain: str, hashed: str) -> bool:
    //             return pwd_context.verify(plain, hashed)
    public bool VerifyPassword(string plain, string hashed) =>
        BCrypt.Net.BCrypt.Verify(plain, hashed);


    // ── JWT TOKEN CREATION ────────────────────────────────────────────
    // Python: def create_access_token(data: dict) -> str:
    //             payload = data.copy()
    //             payload["exp"] = now + timedelta(minutes=30)
    //             return jwt.encode(payload, SECRET_KEY, algorithm="HS256")
    public string CreateAccessToken(string email)
    {
        // Convert secret key string → cryptographic key object
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // "Claim" = a key-value pair inside the JWT payload
        // Python equivalent: payload = {"sub": email}
        var claims = new[] { new Claim("sub", email) };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiresMinutes), // = timedelta(minutes=30)
            signingCredentials: creds
        );

        // Serialize token to string — same as jwt.encode() returning a string
        return new JwtSecurityTokenHandler().WriteToken(token);
    }


    // ── JWT TOKEN VALIDATION ──────────────────────────────────────────
    // Python: payload = jwt.decode(token, SECRET_KEY, algorithms=[ALGORITHM])
    //         return payload["sub"]
    public string? ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secretKey));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,   // same as your Python verify_signature options
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero // no tolerance for expired tokens
            }, out var validatedToken);

            // Extract "sub" claim (the email) from the validated token
            var jwt = (JwtSecurityToken)validatedToken;
            return jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
            // "?." = safe navigation — like Python's: payload.get("sub")
        }
        catch
        {
            return null; // Python equivalent: except JWTError: pass
        }
    }
}
