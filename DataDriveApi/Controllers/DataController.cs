// =====================================================================
// Controllers/DataController.cs — equivalent of app/routers/data.py
// =====================================================================
// Python had:
//   @router.post("/ingest")
//   def ingest_market_data(request: IngestRequest, user=Depends(get_current_user)): ...
//
//   @router.get("/report/latest")
//   def latest_snapshot(limit: int = 50): ...
//
//   @router.get("/report/coin/{coin_id}")
//   def coin_timeseries(coin_id: str): ...
//
// Key C# difference:
//   [Authorize]                = user=Depends(get_current_user)  — requires valid JWT
//   HttpContext.User.FindFirst = payload["sub"]                  — gets email from token
//   HttpClient                 = requests.get()                  — HTTP client
// =====================================================================

using System.Text.Json;
using DataDriveApi.Data;
using DataDriveApi.Models;
using DataDriveApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using System.Security.Claims;

namespace DataDriveApi.Controllers;

[ApiController]
public class DataController : ControllerBase
{
    private readonly Database _db;
    private readonly AuthService _auth;
    private readonly HttpClient _httpClient;

    public DataController(Database db, AuthService auth, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _auth = auth;
        _httpClient = httpClientFactory.CreateClient();
    }

    // ── INGEST ────────────────────────────────────────────────────────
    // Python:
    //   @router.post("/ingest")
    //   def ingest_market_data(request: IngestRequest, user=Depends(get_current_user)):
    //       response = requests.get(request.url, ...)
    //       data = response.json()
    //       for coin in data: c.execute("INSERT INTO market_snapshots ...")
    //       return {"status": "success", "records_ingested": len(data), ...}
    [HttpPost("ingest")]
    [Authorize]  // = user=Depends(get_current_user) — rejects requests without valid JWT
    public async Task<IActionResult> IngestMarketData(IngestRequest req)
    {
        // ── Fetch from external API (= requests.get(...)) ──
        HttpResponseMessage response;
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, req.Url);
            request.Headers.Add("User-Agent", "DataDrive/1.0");
            response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode(); // = response.raise_for_status()
        }
        catch (HttpRequestException ex)
        {
            return BadRequest(new { detail = ex.Message }); // = HTTPException(400, str(e))
        }

        // ── Parse JSON (= response.json()) ──
        var body = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<List<JsonElement>>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (data == null || data.Count == 0 || data[0].ValueKind != JsonValueKind.Object)
            return BadRequest(new { detail = "Expected a list of market objects" });

        // ── Insert rows (= for coin in data: c.execute("INSERT ...")) ──
        var ts = DateTime.UtcNow.ToString("o");
        using var conn = _db.GetConnection();
        foreach (var coin in data)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO market_snapshots (
                    coin_id, symbol, name,
                    current_price, market_cap, total_volume,
                    price_change_24h, price_change_pct_24h,
                    high_24h, low_24h,
                    circulating_supply, max_supply,
                    ath, ath_change_pct, timestamp
                ) VALUES (
                    @coin_id, @symbol, @name,
                    @current_price, @market_cap, @total_volume,
                    @price_change_24h, @price_change_pct_24h,
                    @high_24h, @low_24h,
                    @circulating_supply, @max_supply,
                    @ath, @ath_change_pct, @timestamp
                )";


            // Python equivalent: coin.get("key")
            // GetStr → returns string or DBNull (for text columns)
            // GetNum → returns double or DBNull (for DOUBLE PRECISION columns)
            object GetStr(string key) =>
                coin.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null
                    ? v.ToString() : (object)DBNull.Value;

            object GetNum(string key) =>
                coin.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number
                    ? v.GetDouble() : (object)DBNull.Value;

            cmd.Parameters.AddWithValue("coin_id",              GetStr("id"));
            cmd.Parameters.AddWithValue("symbol",               GetStr("symbol"));
            cmd.Parameters.AddWithValue("name",                 GetStr("name"));
            cmd.Parameters.AddWithValue("current_price",        GetNum("current_price"));
            cmd.Parameters.AddWithValue("market_cap",           GetNum("market_cap"));
            cmd.Parameters.AddWithValue("total_volume",         GetNum("total_volume"));
            cmd.Parameters.AddWithValue("price_change_24h",     GetNum("price_change_24h"));
            cmd.Parameters.AddWithValue("price_change_pct_24h", GetNum("price_change_percentage_24h"));
            cmd.Parameters.AddWithValue("high_24h",             GetNum("high_24h"));
            cmd.Parameters.AddWithValue("low_24h",              GetNum("low_24h"));
            cmd.Parameters.AddWithValue("circulating_supply",   GetNum("circulating_supply"));
            cmd.Parameters.AddWithValue("max_supply",           GetNum("max_supply"));
            cmd.Parameters.AddWithValue("ath",                  GetNum("ath"));
            cmd.Parameters.AddWithValue("ath_change_pct",       GetNum("ath_change_percentage"));
            cmd.Parameters.AddWithValue("timestamp",            ts);
            cmd.ExecuteNonQuery();

        }

        return Ok(new IngestResponse("success", data.Count, ts));
    }


    // ── LATEST SNAPSHOT ───────────────────────────────────────────────
    // Python:
    //   @router.get("/report/latest")
    //   def latest_snapshot(limit: int = 50):
    //       cursor.execute("SELECT * FROM market_snapshots WHERE timestamp = MAX ORDER BY market_cap DESC")
    //       return cursor.fetchall()
    [HttpGet("report/latest")]
    public IActionResult LatestSnapshot([FromQuery] int limit = 50) // [FromQuery] = query param like ?limit=50
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM market_snapshots
            WHERE timestamp = (SELECT MAX(timestamp) FROM market_snapshots)
            ORDER BY market_cap DESC
            LIMIT @limit";
        cmd.Parameters.AddWithValue("limit", limit);

        var rows = new List<Dictionary<string, object?>>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) // = for row in cursor.fetchall()
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return Ok(rows);
    }


    // ── COIN TIMESERIES ───────────────────────────────────────────────
    // Python:
    //   @router.get("/report/coin/{coin_id}")
    //   def coin_timeseries(coin_id: str):
    //       cursor.execute("SELECT timestamp, current_price... WHERE coin_id=%s ORDER BY timestamp")
    //       return cursor.fetchall()
    [HttpGet("report/coin/{coinId}")] // {coinId} in URL = {coin_id} path param in Python
    public IActionResult CoinTimeseries(string coinId)
    {
        using var conn = _db.GetConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT timestamp, current_price, market_cap, total_volume
            FROM market_snapshots
            WHERE coin_id = @coinId
            ORDER BY timestamp";
        cmd.Parameters.AddWithValue("coinId", coinId);

        var rows = new List<Dictionary<string, object?>>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return Ok(rows);
    }
}
