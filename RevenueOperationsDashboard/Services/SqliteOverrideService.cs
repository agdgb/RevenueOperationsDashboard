using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using RevenueOperationsDashboard.Models;

namespace RevenueOperationsDashboard.Services
{
    public class SqliteOverrideService
    {
        private readonly string _connectionString;
        private readonly ILogger<SqliteOverrideService> _logger;
        private static readonly SemaphoreSlim _initLock = new(1, 1);
        private static bool _isInitialized = false;

        public static readonly string[] KnownCards = new[] { "kpi1", "kpi2", "kpi3", "kpi4", "kpi5", "kpi7" };

        public SqliteOverrideService(IWebHostEnvironment env, ILogger<SqliteOverrideService> logger)
        {
            _logger = logger;
            var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            var dbPath = Path.Combine(dataDir, "dashboard_overrides.db");
            _connectionString = $"Data Source={dbPath};";
        }

        private async Task EnsureDatabaseInitializedAsync()
        {
            if (_isInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync();

                const string initSql = @"
                    CREATE TABLE IF NOT EXISTS SystemSettings (
                        SettingKey TEXT PRIMARY KEY,
                        SettingValue TEXT NOT NULL,
                        UpdatedAt TEXT NOT NULL,
                        UpdatedBy TEXT NULL
                    );

                    CREATE TABLE IF NOT EXISTS TopCardOverrides (
                        CardId TEXT NOT NULL,
                        FiscalYearId TEXT NOT NULL,
                        IsEnabled INTEGER NOT NULL DEFAULT 0,
                        Actual REAL NULL,
                        Target REAL NULL,
                        UseHumanFormat INTEGER NOT NULL DEFAULT 1,
                        ApplyOnlyAllBranches INTEGER NOT NULL DEFAULT 1,
                        Notes TEXT NULL,
                        UpdatedAt TEXT NOT NULL,
                        UpdatedBy TEXT NULL,
                        PRIMARY KEY (CardId, FiscalYearId)
                    );

                    INSERT OR IGNORE INTO SystemSettings (SettingKey, SettingValue, UpdatedAt, UpdatedBy)
                    VALUES ('TopCardsMode', 'Hybrid', datetime('now'), 'System');
                ";

                await conn.ExecuteAsync(initSql);

                // Safe migration: check if UseHumanFormat column exists
                try
                {
                    await conn.ExecuteAsync("ALTER TABLE TopCardOverrides ADD COLUMN UseHumanFormat INTEGER NOT NULL DEFAULT 1;");
                }
                catch
                {
                    // Column already exists
                }

                _isInitialized = true;
                _logger.LogInformation("SQLite override database initialized successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize SQLite override database.");
                throw;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task<SqliteConnection> GetConnectionAsync()
        {
            await EnsureDatabaseInitializedAsync();
            var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        public async Task<string> GetTopCardsModeAsync()
        {
            try
            {
                using var conn = await GetConnectionAsync();
                var mode = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT SettingValue FROM SystemSettings WHERE SettingKey = 'TopCardsMode';"
                );
                return string.IsNullOrWhiteSpace(mode) ? "Hybrid" : mode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading TopCardsMode from SQLite. Falling back to 'Hybrid'.");
                return "Hybrid";
            }
        }

        public async Task SetTopCardsModeAsync(string mode, string? updatedBy = "User")
        {
            using var conn = await GetConnectionAsync();
            const string sql = @"
                INSERT INTO SystemSettings (SettingKey, SettingValue, UpdatedAt, UpdatedBy)
                VALUES ('TopCardsMode', @Mode, datetime('now'), @UpdatedBy)
                ON CONFLICT(SettingKey) DO UPDATE SET
                    SettingValue = excluded.SettingValue,
                    UpdatedAt = excluded.UpdatedAt,
                    UpdatedBy = excluded.UpdatedBy;
            ";
            await conn.ExecuteAsync(sql, new { Mode = mode, UpdatedBy = updatedBy });
        }

        public async Task<List<TopCardOverrideDto>> GetOverridesAsync(string fiscalYearId)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                const string sql = @"
                    SELECT 
                        CardId,
                        FiscalYearId,
                        IsEnabled,
                        CAST(Actual AS TEXT) AS ActualRaw,
                        CAST(Target AS TEXT) AS TargetRaw,
                        UseHumanFormat,
                        ApplyOnlyAllBranches,
                        Notes,
                        UpdatedAt,
                        UpdatedBy
                    FROM TopCardOverrides
                    WHERE FiscalYearId = @FiscalYearId OR FiscalYearId = 'ALL'
                    ORDER BY CASE WHEN FiscalYearId = @FiscalYearId THEN 0 ELSE 1 END;
                ";
                var rows = (await conn.QueryAsync<SqliteTopCardRow>(sql, new { FiscalYearId = fiscalYearId })).ToList();
                var map = new Dictionary<string, TopCardOverrideDto>(StringComparer.OrdinalIgnoreCase);

                foreach (var r in rows)
                {
                    if (map.ContainsKey(r.CardId)) continue;

                    decimal? act = decimal.TryParse(r.ActualRaw, out var a) ? a : null;
                    decimal? tgt = decimal.TryParse(r.TargetRaw, out var t) ? t : null;
                    map[r.CardId] = new TopCardOverrideDto
                    {
                        CardId = r.CardId,
                        FiscalYearId = r.FiscalYearId,
                        IsEnabled = r.IsEnabled == 1,
                        Actual = act,
                        Target = tgt,
                        UseHumanFormat = r.UseHumanFormat == 1,
                        ApplyOnlyAllBranches = r.ApplyOnlyAllBranches == 1,
                        Notes = r.Notes,
                        UpdatedAt = DateTime.TryParse(r.UpdatedAt, out var dt) ? dt : DateTime.UtcNow,
                        UpdatedBy = r.UpdatedBy
                    };
                }

                // Ensure all known cards exist in the returned list
                var list = new List<TopCardOverrideDto>();
                foreach (var cardId in KnownCards)
                {
                    if (map.TryGetValue(cardId, out var existing))
                    {
                        list.Add(existing);
                    }
                    else
                    {
                        // Default fallback: Tax Audit (kpi7) defaults to Exact (UseHumanFormat = false), others default to Human (true)
                        bool defaultHuman = cardId != "kpi7";
                        list.Add(new TopCardOverrideDto
                        {
                            CardId = cardId,
                            FiscalYearId = fiscalYearId,
                            IsEnabled = false,
                            Actual = null,
                            Target = null,
                            UseHumanFormat = defaultHuman,
                            ApplyOnlyAllBranches = true,
                            UpdatedAt = DateTime.UtcNow,
                            UpdatedBy = "SystemDefault"
                        });
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching overrides for FiscalYearId {FiscalYearId}", fiscalYearId);
                return new List<TopCardOverrideDto>();
            }
        }

        public async Task<Dictionary<string, TopCardOverrideDto>> GetActiveOverridesLookupAsync(string fiscalYearId)
        {
            var list = await GetOverridesAsync(fiscalYearId);
            return list.ToDictionary(k => k.CardId, v => v, StringComparer.OrdinalIgnoreCase);
        }

        public async Task SaveOverridesAsync(string fiscalYearId, List<TopCardOverrideDto> overrides, string? updatedBy = "User")
        {
            using var conn = await GetConnectionAsync();
            using var tx = conn.BeginTransaction();
            try
            {
                const string upsertSql = @"
                    INSERT INTO TopCardOverrides (
                        CardId, FiscalYearId, IsEnabled, Actual, Target, UseHumanFormat, ApplyOnlyAllBranches, Notes, UpdatedAt, UpdatedBy
                    ) VALUES (
                        @CardId, @FiscalYearId, @IsEnabled, @Actual, @Target, @UseHumanFormat, @ApplyOnlyAllBranches, @Notes, datetime('now'), @UpdatedBy
                    )
                    ON CONFLICT(CardId, FiscalYearId) DO UPDATE SET
                        IsEnabled = excluded.IsEnabled,
                        Actual = excluded.Actual,
                        Target = excluded.Target,
                        UseHumanFormat = excluded.UseHumanFormat,
                        ApplyOnlyAllBranches = excluded.ApplyOnlyAllBranches,
                        Notes = excluded.Notes,
                        UpdatedAt = excluded.UpdatedAt,
                        UpdatedBy = excluded.UpdatedBy;
                ";

                foreach (var item in overrides)
                {
                    await conn.ExecuteAsync(upsertSql, new
                    {
                        CardId = item.CardId,
                        FiscalYearId = fiscalYearId,
                        IsEnabled = item.IsEnabled ? 1 : 0,
                        Actual = item.Actual.HasValue ? (double?)item.Actual.Value : null,
                        Target = item.Target.HasValue ? (double?)item.Target.Value : null,
                        UseHumanFormat = item.UseHumanFormat ? 1 : 0,
                        ApplyOnlyAllBranches = item.ApplyOnlyAllBranches ? 1 : 0,
                        Notes = item.Notes,
                        UpdatedBy = updatedBy
                    }, transaction: tx);
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _logger.LogError(ex, "Failed to save overrides in SQLite for FiscalYearId {FiscalYearId}", fiscalYearId);
                throw;
            }
        }

        public async Task<TopCardSettingsStateDto> GetFullSettingsStateAsync(string fiscalYearId)
        {
            var mode = await GetTopCardsModeAsync();
            var overrides = await GetOverridesAsync(fiscalYearId);
            return new TopCardSettingsStateDto
            {
                TopCardsMode = mode,
                FiscalYearId = fiscalYearId,
                Overrides = overrides
            };
        }

        private class SqliteTopCardRow
        {
            public string CardId { get; set; } = string.Empty;
            public string FiscalYearId { get; set; } = string.Empty;
            public long IsEnabled { get; set; }
            public string? ActualRaw { get; set; }
            public string? TargetRaw { get; set; }
            public long UseHumanFormat { get; set; }
            public long ApplyOnlyAllBranches { get; set; }
            public string? Notes { get; set; }
            public string? UpdatedAt { get; set; }
            public string? UpdatedBy { get; set; }
        }
    }
}
