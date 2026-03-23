// =====================================================================
// Data/Database.cs — equivalent of app/database.py
// =====================================================================
// Python had:
//   def get_conn(): return psycopg2.connect(DATABASE_URL)
//   def init_db(): conn=get_conn(); c=conn.cursor(); c.execute(CREATE TABLE...); conn.commit()
//
// C# equivalent uses Npgsql — the .NET PostgreSQL driver.
// The API is very similar to psycopg2:
//   NpgsqlConnection  ≈  psycopg2 connection
//   NpgsqlCommand     ≈  cursor + execute
//   ExecuteNonQuery() ≈  cursor.execute() for INSERT/UPDATE/CREATE
//   ExecuteReader()   ≈  cursor.fetchall()
// =====================================================================

using Npgsql;
using DataDriveApi.Models;

namespace DataDriveApi.Data;

public class Database
{
    private readonly string _connectionString;

    public Database(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ── GET CONNECTION ────────────────────────────────────────────────
    // Python: def get_conn(): return psycopg2.connect(DATABASE_URL)
    public NpgsqlConnection GetConnection()
    {
        var conn = new NpgsqlConnection(_connectionString);
        conn.Open();  // psycopg2 connects on creation; Npgsql needs explicit Open()
        return conn;
    }

    // ── INIT TABLES ───────────────────────────────────────────────────
    // Python: def init_db(): ... c.execute("CREATE TABLE IF NOT EXISTS market_snapshots ...")
    public void InitDb()
    {
        // "using" = Python's "with" statement — auto-closes connection when done
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS market_snapshots (
                id                  SERIAL PRIMARY KEY,
                coin_id             VARCHAR(100),
                symbol              VARCHAR(20),
                name                VARCHAR(100),
                current_price       DOUBLE PRECISION,
                market_cap          DOUBLE PRECISION,
                total_volume        DOUBLE PRECISION,
                price_change_24h    DOUBLE PRECISION,
                price_change_pct_24h DOUBLE PRECISION,
                high_24h            DOUBLE PRECISION,
                low_24h             DOUBLE PRECISION,
                circulating_supply  DOUBLE PRECISION,
                max_supply          DOUBLE PRECISION,
                ath                 DOUBLE PRECISION,
                ath_change_pct      DOUBLE PRECISION,
                timestamp           VARCHAR(50)
            )";
        cmd.ExecuteNonQuery(); // = c.execute(...) + conn.commit() combined for DDL
    }

    // Python: def init_db_users(): ... c.execute("CREATE TABLE IF NOT EXISTS users ...")
    public void InitDbUsers()
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS users (
                id              SERIAL PRIMARY KEY,
                email           VARCHAR(255) UNIQUE,
                hashed_password TEXT,
                created_at      VARCHAR(50)
            )";
        cmd.ExecuteNonQuery();
    }


    // ── USER QUERIES ──────────────────────────────────────────────────
    // Python: def get_user_from_db(email): cursor.execute("SELECT * FROM users WHERE email=%s")
    //         return cursor.fetchone()   # returns a dict-like row
    public UserRecord? GetUserByEmail(string email)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, email, hashed_password, created_at FROM users WHERE email = @email";
        cmd.Parameters.AddWithValue("email", email); // = (%s, email) — named params in C#

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null; // = if not row: return None

        return new UserRecord(
            reader.GetInt32(0),    // id
            reader.GetString(1),   // email
            reader.GetString(2),   // hashed_password
            reader.GetString(3)    // created_at
        );
    }

    // Python: c.execute("INSERT INTO users (email, hashed_password, created_at) VALUES (%s,%s,%s)", ...)
    public void CreateUser(string email, string hashedPassword)
    {
        using var conn = GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO users (email, hashed_password, created_at)
            VALUES (@email, @hashed, @createdAt)";
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("hashed", hashedPassword);
        cmd.Parameters.AddWithValue("createdAt", DateTime.UtcNow.ToString("o")); // ISO format
        cmd.ExecuteNonQuery();
    }
}
