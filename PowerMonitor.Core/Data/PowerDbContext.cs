using Microsoft.Data.Sqlite;
using PowerMonitor.Core.Models;

namespace PowerMonitor.Core.Data;

public class PowerDbContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public PowerDbContext(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (dir != null) Directory.CreateDirectory(dir);
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS power_samples (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                timestamp TEXT NOT NULL,
                total_watts REAL NOT NULL,
                sensor_json TEXT NOT NULL DEFAULT '[]',
                price_per_kwh REAL NOT NULL DEFAULT 0.6
            );
            CREATE TABLE IF NOT EXISTS config (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_samples_timestamp ON power_samples(timestamp);
        ";
        cmd.ExecuteNonQuery();
    }

    public void InsertSample(PowerSample sample)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO power_samples (timestamp, total_watts, sensor_json, price_per_kwh)
            VALUES (@ts, @tw, @sj, @ppk)";
        cmd.Parameters.AddWithValue("@ts", sample.Timestamp.ToString("o"));
        cmd.Parameters.AddWithValue("@tw", sample.TotalWatts);
        cmd.Parameters.AddWithValue("@sj", sample.SensorJson);
        cmd.Parameters.AddWithValue("@ppk", sample.PricePerKwh);
        cmd.ExecuteNonQuery();
    }

    public List<PowerSample> GetSamples(DateTime from, DateTime to)
    {
        var samples = new List<PowerSample>();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, timestamp, total_watts, sensor_json, price_per_kwh
            FROM power_samples
            WHERE timestamp >= @from AND timestamp <= @to
            ORDER BY timestamp ASC";
        cmd.Parameters.AddWithValue("@from", from.ToString("o"));
        cmd.Parameters.AddWithValue("@to", to.ToString("o"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            samples.Add(new PowerSample
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTime.Parse(reader.GetString(1)),
                TotalWatts = reader.GetDouble(2),
                SensorJson = reader.GetString(3),
                PricePerKwh = reader.GetDouble(4)
            });
        }
        return samples;
    }

    public PowerSample? GetLatestSample()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, timestamp, total_watts, sensor_json, price_per_kwh
            FROM power_samples
            ORDER BY id DESC LIMIT 1";

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new PowerSample
            {
                Id = reader.GetInt64(0),
                Timestamp = DateTime.Parse(reader.GetString(1)),
                TotalWatts = reader.GetDouble(2),
                SensorJson = reader.GetString(3),
                PricePerKwh = reader.GetDouble(4)
            };
        }
        return null;
    }

    public void DeleteSamplesOlderThan(DateTime cutoff)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM power_samples WHERE timestamp < @cutoff";
        cmd.Parameters.AddWithValue("@cutoff", cutoff.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public string GetConfig(string key, string defaultValue = "")
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM config WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", key);
        var result = cmd.ExecuteScalar();
        return result?.ToString() ?? defaultValue;
    }

    public void SetConfig(string key, string value)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO config (key, value, updated_at)
            VALUES (@key, @value, @now)";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
