using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using RevenueOperationsDashboard.Models;

namespace RevenueOperationsDashboard.Services
{
    public class SqliteOverrideService
    {
        private readonly List<string> _candidateDbPaths = new();
        private readonly ILogger<SqliteOverrideService> _logger;
        private static readonly SemaphoreSlim _initLock = new(1, 1);
        private static bool _isInitialized = false;
        private static bool _isAvailable = true;
        private static string? _lastInitError;
        private string? _activeDbPath;
        private string? _activeConnectionString;

        public static readonly string[] KnownCards = new[] { "kpi1", "kpi2", "kpi3", "kpi4", "kpi5", "kpi7" };

        public string DatabasePath => _activeDbPath ?? _candidateDbPaths.FirstOrDefault() ?? "Unknown";

        public SqliteOverrideService(IWebHostEnvironment env, ILogger<SqliteOverrideService> logger)
        {
            _logger = logger;

            // In IIS, AppContext.BaseDirectory is the actual deployed application directory.
            // env.ContentRootPath might point to C:\Windows\System32\inetsrv in some IIS configurations.
            if (!string.IsNullOrWhiteSpace(AppContext.BaseDirectory))
            {
                _candidateDbPaths.Add(Path.Combine(AppContext.BaseDirectory, "App_Data", "dashboard_overrides.db"));
            }
            if (!string.IsNullOrWhiteSpace(env.ContentRootPath))
            {
                var envPath = Path.Combine(env.ContentRootPath, "App_Data", "dashboard_overrides.db");
                if (!_candidateDbPaths.Contains(envPath, StringComparer.OrdinalIgnoreCase))
                {
                    _candidateDbPaths.Add(envPath);
                }
            }
            _candidateDbPaths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RevenueOperationsDashboard", "App_Data", "dashboard_overrides.db"));
            _candidateDbPaths.Add(Path.Combine(Path.GetTempPath(), "RevenueOperationsDashboard", "App_Data", "dashboard_overrides.db"));
        }

        private async Task EnsureDatabaseInitializedAsync()
        {
            if (_isInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                foreach (var dbPath in _candidateDbPaths)
                {
                    try
                    {
                        var dir = Path.GetDirectoryName(dbPath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }

                        // Verify write access to directory
                        var probeFile = Path.Combine(dir!, $".write_test_{Guid.NewGuid():N}");
                        File.WriteAllText(probeFile, "ok");
                        File.Delete(probeFile);

                        var builder = new SqliteConnectionStringBuilder
                        {
                            DataSource = dbPath,
                            Mode = SqliteOpenMode.ReadWriteCreate,
                            Cache = SqliteCacheMode.Shared,
                            DefaultTimeout = 5
                        };
                        var connStr = builder.ToString();

                        using (var conn = new SqliteConnection(connStr))
                        {
                            await conn.OpenAsync();

                            try
                            {
                                await conn.ExecuteAsync("PRAGMA journal_mode = WAL;");
                            }
                            catch
                            {
                                // Fallback to default journal mode if WAL shared memory is restricted by IIS
                            }
                            await conn.ExecuteAsync("PRAGMA busy_timeout = 5000;");

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
                        }

                        _activeDbPath = dbPath;
                        _activeConnectionString = connStr;
                        _isInitialized = true;
                        _isAvailable = true;
                        _lastInitError = null;
                        _logger.LogInformation("SQLite override database initialized successfully at: {Path}", dbPath);
                        return;
                    }
                    catch (Exception ex)
                    {
                        _lastInitError = ex.Message;
                        _logger.LogWarning(ex, "Failed to initialize SQLite at candidate path {Path}. Trying next candidate...", dbPath);
                    }
                }

                // If all candidates failed
                _isInitialized = true;
                _isAvailable = false;
                _logger.LogError("All SQLite candidates failed. Operating in pure SQL Server fallback mode. Last error: {Error}", _lastInitError);
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task<SqliteConnection?> GetConnectionAsync()
        {
            await EnsureDatabaseInitializedAsync();
            if (!_isAvailable || string.IsNullOrEmpty(_activeConnectionString)) return null;

            try
            {
                var conn = new SqliteConnection(_activeConnectionString);
                await conn.OpenAsync();
                return conn;
            }
            catch (Exception ex)
            {
                _lastInitError = ex.Message;
                _logger.LogError(ex, "Failed to open SQLite connection to {Path}. Operating in fallback mode.", DatabasePath);
                return null;
            }
        }

        public async Task<string> GetTopCardsModeAsync()
        {
            try
            {
                using var conn = await GetConnectionAsync();
                if (conn == null) return "Auto";

                var mode = await conn.QueryFirstOrDefaultAsync<string>(
                    "SELECT SettingValue FROM SystemSettings WHERE SettingKey = 'TopCardsMode';"
                );
                return string.IsNullOrWhiteSpace(mode) ? "Hybrid" : mode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading TopCardsMode from SQLite. Falling back to 'Auto'.");
                return "Auto";
            }
        }

        public async Task SetTopCardsModeAsync(string mode, string? updatedBy = "User")
        {
            try
            {
                using var conn = await GetConnectionAsync();
                if (conn == null)
                {
                    _logger.LogWarning("SQLite is unavailable; cannot set TopCardsMode.");
                    return;
                }

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set TopCardsMode in SQLite.");
                throw new InvalidOperationException($"Failed to update TopCardsMode in SQLite at '{DatabasePath}': {ex.Message}", ex);
            }
        }

        public async Task<List<TopCardOverrideDto>> GetOverridesAsync(string fiscalYearId)
        {
            try
            {
                using var conn = await GetConnectionAsync();
                if (conn == null)
                {
                    return GetDefaultOverrides(fiscalYearId);
                }

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

                var list = new List<TopCardOverrideDto>();
                foreach (var cardId in KnownCards)
                {
                    if (map.TryGetValue(cardId, out var existing))
                    {
                        list.Add(existing);
                    }
                    else
                    {
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
                _logger.LogError(ex, "Error fetching overrides for FiscalYearId {FiscalYearId}. Falling back to defaults.", fiscalYearId);
                return GetDefaultOverrides(fiscalYearId);
            }
        }

        private List<TopCardOverrideDto> GetDefaultOverrides(string fiscalYearId)
        {
            var list = new List<TopCardOverrideDto>();
            foreach (var cardId in KnownCards)
            {
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
            return list;
        }

        public async Task<Dictionary<string, TopCardOverrideDto>> GetActiveOverridesLookupAsync(string fiscalYearId)
        {
            var list = await GetOverridesAsync(fiscalYearId);
            return list.ToDictionary(k => k.CardId, v => v, StringComparer.OrdinalIgnoreCase);
        }

        public async Task SaveOverridesAsync(string fiscalYearId, List<TopCardOverrideDto> overrides, string? updatedBy = "User")
        {
            using var conn = await GetConnectionAsync();
            if (conn == null)
            {
                throw new InvalidOperationException($"SQLite storage is not writable at '{DatabasePath}'. Reason: {_lastInitError ?? "Could not establish database connection."}");
            }

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
                try { tx.Rollback(); } catch { }
                _logger.LogError(ex, "Failed to save overrides in SQLite at {Path} for FiscalYearId {FiscalYearId}", DatabasePath, fiscalYearId);
                throw new InvalidOperationException($"Failed to write overrides to SQLite at '{DatabasePath}': {ex.Message}", ex);
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
