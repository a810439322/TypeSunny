using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using Newtonsoft.Json;
using TypeSunny.Logs;

namespace TypeSunny.Personalization
{
    /// <summary>
    /// 个人化打字画像的 SQLite 实现。
    ///
    /// 设计要点：
    /// - 懒打开：构造函数只记路径，第一次 Load/Save 时才打开连接 + 建表 + 跑迁移。
    /// - 线程安全：所有 DB 操作串行化在 syncRoot 上，因为 System.Data.SQLite 的 SQLiteConnection
    ///   并非线程安全；读写多在 UI 线程或后台 Train 线程，单锁足以串行。
    /// - 迁移：第一次打开时若同目录存在旧 PersonalTypingProfile.json，会反序列化成 LegacyJsonProfile
    ///   并在临时 db 文件中单事务批量插入，写完后原子 File.Move 到目标路径再删 JSON。失败保留 JSON。
    /// - WAL：开 WAL + synchronous=NORMAL，写不阻塞读，崩溃只丢最后几条 commit。
    /// </summary>
    internal sealed class SqlitePersonalTypingProfileStore : IPersonalTypingProfileStore
    {
        private const int SQLiteMaxParameters = 900; // 安全余量，标准上限 999

        private readonly string dbPath;
        private readonly object syncRoot = new object();
        private SQLiteConnection conn;
        private bool disposed;

        public SqlitePersonalTypingProfileStore(string dbPath)
        {
            if (string.IsNullOrEmpty(dbPath))
                throw new ArgumentException("dbPath is required", "dbPath");
            this.dbPath = dbPath;
        }

        public PersonalTypingProfile Load()
        {
            // 兼容历史语义：把全部 units 一次性读出来。
            // 该入口仅用于测试 setup 与离线工具；运行时优先用 LoadWithUnits。
            lock (syncRoot)
            {
                EnsureOpen();
                var profile = new PersonalTypingProfile();
                LoadBaselineInto(profile);
                LoadCalibrationInto(profile);
                profile.Units = LoadAllUnits();
                return profile;
            }
        }

        private Dictionary<string, PersonalTypingUnitStats> LoadAllUnits()
        {
            var result = new Dictionary<string, PersonalTypingUnitStats>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT text, count, observed_chars, total_ms, total_keys,
                                           lt_ms, lt_keys, lt_weight, rec_ms, rec_keys, rec_weight
                                    FROM units;";
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var u = new PersonalTypingUnitStats
                        {
                            Text = r.GetString(0),
                            Count = r.GetInt32(1),
                            ObservedCharacters = r.GetInt32(2),
                            TotalMilliseconds = r.GetDouble(3),
                            TotalKeys = r.GetDouble(4),
                            LongTermWeightedMilliseconds = r.GetDouble(5),
                            LongTermWeightedKeys = r.GetDouble(6),
                            LongTermWeight = r.GetDouble(7),
                            RecencyWeightedMilliseconds = r.GetDouble(8),
                            RecencyWeightedKeys = r.GetDouble(9),
                            RecencyWeight = r.GetDouble(10)
                        };
                        result[u.Text] = u;
                    }
                }
            }
            return result;
        }

        public PersonalTypingProfile LoadWithUnits(IEnumerable<string> texts)
        {
            lock (syncRoot)
            {
                EnsureOpen();
                var profile = new PersonalTypingProfile();
                LoadBaselineInto(profile);
                LoadCalibrationInto(profile);
                profile.Units = LoadUnits(texts);
                return profile;
            }
        }

        public void Save(PersonalTypingProfile profile)
        {
            if (profile == null)
                return;

            lock (syncRoot)
            {
                EnsureOpen();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        WriteBaseline(conn, tx, profile);
                        WriteCalibration(conn, tx, profile.Calibration ?? new PersonalPredictionCalibration());

                        // 全量保存的语义：清掉旧 units，再批量写入
                        ExecuteNonQuery(conn, tx, "DELETE FROM units;");
                        if (profile.Units != null && profile.Units.Count > 0)
                            UpsertUnits(conn, tx, profile.Units.Values);

                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public void ApplyTraining(PersonalTypingProfile profile)
        {
            if (profile == null)
                return;

            lock (syncRoot)
            {
                EnsureOpen();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        WriteBaseline(conn, tx, profile);
                        if (profile.Units != null && profile.Units.Count > 0)
                            UpsertUnits(conn, tx, profile.Units.Values);
                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public void ApplyCalibration(PersonalPredictionCalibration calibration)
        {
            if (calibration == null)
                return;

            lock (syncRoot)
            {
                EnsureOpen();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        WriteCalibration(conn, tx, calibration);
                        tx.Commit();
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                if (disposed)
                    return;
                disposed = true;
                if (conn != null)
                {
                    try { conn.Close(); } catch { }
                    try { conn.Dispose(); } catch { }
                    conn = null;
                }
            }
        }

        // ---------------------------------------------------------------------
        // open / migrate / schema
        // ---------------------------------------------------------------------

        private void EnsureOpen()
        {
            if (disposed)
                throw new ObjectDisposedException("SqlitePersonalTypingProfileStore");
            if (conn != null)
                return;

            EnsureDirectoryExists(dbPath);
            TryMigrateFromJson();

            conn = new SQLiteConnection(BuildConnectionString(dbPath));
            conn.Open();
            ApplyPragmas(conn);
            EnsureSchema(conn);
        }

        private static string BuildConnectionString(string filePath)
        {
            // 显式不开 Pooling，避免文件句柄被 connection pool 占着，关闭后 db-wal/db-shm 才能被回收。
            return "Data Source=" + filePath + ";Version=3;Pooling=False;";
        }

        private static void ApplyPragmas(SQLiteConnection c)
        {
            ExecuteNonQuery(c, null, "PRAGMA journal_mode = WAL;");
            ExecuteNonQuery(c, null, "PRAGMA synchronous = NORMAL;");
            ExecuteNonQuery(c, null, "PRAGMA temp_store = MEMORY;");
            ExecuteNonQuery(c, null, "PRAGMA cache_size = -8000;");
        }

        private static void EnsureSchema(SQLiteConnection c)
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS meta (
  key TEXT PRIMARY KEY,
  value TEXT
);
INSERT OR IGNORE INTO meta (key, value) VALUES ('schema_version', '1');

CREATE TABLE IF NOT EXISTS baseline (
  singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
  effective_chars      INTEGER NOT NULL DEFAULT 0,
  speed                REAL    NOT NULL DEFAULT 120,
  hit_rate             REAL    NOT NULL DEFAULT 5,
  kpw                  REAL    NOT NULL DEFAULT 4,
  accuracy             REAL    NOT NULL DEFAULT 98,
  backs_per_char       REAL    NOT NULL DEFAULT 0,
  correction_per_char  REAL    NOT NULL DEFAULT 0,
  waste_per_char       REAL    NOT NULL DEFAULT 0,
  choose_per_char      REAL    NOT NULL DEFAULT 0
);
INSERT OR IGNORE INTO baseline (singleton) VALUES (1);

CREATE TABLE IF NOT EXISTS calibration (
  singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
  count                INTEGER NOT NULL DEFAULT 0,
  observed_chars       INTEGER NOT NULL DEFAULT 0,
  lt_time_ratio        REAL    NOT NULL DEFAULT 0,
  lt_time_weight       REAL    NOT NULL DEFAULT 0,
  rec_time_ratio       REAL    NOT NULL DEFAULT 0,
  rec_time_weight      REAL    NOT NULL DEFAULT 0,
  lt_key_ratio         REAL    NOT NULL DEFAULT 0,
  lt_key_weight        REAL    NOT NULL DEFAULT 0,
  rec_key_ratio        REAL    NOT NULL DEFAULT 0,
  rec_key_weight       REAL    NOT NULL DEFAULT 0
);
INSERT OR IGNORE INTO calibration (singleton) VALUES (1);

CREATE TABLE IF NOT EXISTS units (
  text                 TEXT    PRIMARY KEY,
  count                INTEGER NOT NULL DEFAULT 0,
  observed_chars       INTEGER NOT NULL DEFAULT 0,
  total_ms             REAL    NOT NULL DEFAULT 0,
  total_keys           REAL    NOT NULL DEFAULT 0,
  lt_ms                REAL    NOT NULL DEFAULT 0,
  lt_keys              REAL    NOT NULL DEFAULT 0,
  lt_weight            REAL    NOT NULL DEFAULT 0,
  rec_ms               REAL    NOT NULL DEFAULT 0,
  rec_keys             REAL    NOT NULL DEFAULT 0,
  rec_weight           REAL    NOT NULL DEFAULT 0,
  last_seen            INTEGER NOT NULL DEFAULT 0
);
";
            ExecuteNonQuery(c, null, sql);
        }

        private void TryMigrateFromJson()
        {
            if (File.Exists(dbPath))
                return;

            string folder = Path.GetDirectoryName(dbPath);
            string jsonPath = string.IsNullOrEmpty(folder)
                ? "PersonalTypingProfile.json"
                : Path.Combine(folder, "PersonalTypingProfile.json");

            if (!File.Exists(jsonPath))
                return;

            string tmpDbPath = dbPath + ".migrating";
            try
            {
                if (File.Exists(tmpDbPath))
                {
                    try { File.Delete(tmpDbPath); } catch { }
                }

                LegacyJsonProfile legacy = null;
                try
                {
                    string json = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                    if (!string.IsNullOrWhiteSpace(json))
                        legacy = JsonConvert.DeserializeObject<LegacyJsonProfile>(json);
                }
                catch (Exception ex)
                {
                    SafeLog("[Profile] migrate: bad json, drop file: " + ex);
                    try { File.Delete(jsonPath); } catch { }
                    return;
                }

                if (legacy == null)
                {
                    // 空 JSON：删掉，按全新 DB 走
                    try { File.Delete(jsonPath); } catch { }
                    return;
                }

                using (var tmpConn = new SQLiteConnection(BuildConnectionString(tmpDbPath)))
                {
                    tmpConn.Open();
                    // 迁移阶段刻意不开 WAL：避免 tmpDbPath-wal/-shm 在 File.Move 后变成孤儿。
                    // 切换到正式 db 路径后由 EnsureOpen → ApplyPragmas 再设 WAL。
                    ExecuteNonQuery(tmpConn, null, "PRAGMA journal_mode = DELETE;");
                    ExecuteNonQuery(tmpConn, null, "PRAGMA synchronous = NORMAL;");
                    ExecuteNonQuery(tmpConn, null, "PRAGMA temp_store = MEMORY;");
                    EnsureSchema(tmpConn);
                    using (var tx = tmpConn.BeginTransaction())
                    {
                        // 把 LegacyJsonProfile 直接当 PersonalTypingProfile 来写
                        var profile = new PersonalTypingProfile
                        {
                            EffectiveStatCharacters = legacy.EffectiveStatCharacters,
                            BaselineSpeed = legacy.BaselineSpeed,
                            BaselineHitRate = legacy.BaselineHitRate,
                            BaselineKpw = legacy.BaselineKpw,
                            BaselineAccuracy = legacy.BaselineAccuracy,
                            BaselineBacksPerChar = legacy.BaselineBacksPerChar,
                            BaselineCorrectionPerChar = legacy.BaselineCorrectionPerChar,
                            BaselineWasteCodesPerChar = legacy.BaselineWasteCodesPerChar,
                            BaselineChoosePerChar = legacy.BaselineChoosePerChar,
                            Calibration = legacy.Calibration ?? new PersonalPredictionCalibration(),
                            Units = legacy.Units ?? new Dictionary<string, PersonalTypingUnitStats>()
                        };

                        WriteBaseline(tmpConn, tx, profile);
                        WriteCalibration(tmpConn, tx, profile.Calibration);
                        if (profile.Units.Count > 0)
                            UpsertUnits(tmpConn, tx, profile.Units.Values);
                        ExecuteNonQuery(tmpConn, tx,
                            "INSERT OR REPLACE INTO meta (key, value) VALUES ('migrated_from_json', '" +
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "');");
                        tx.Commit();
                    }
                    tmpConn.Close();
                }

                // 原子替换
                File.Move(tmpDbPath, dbPath);

                // 迁移成功才删 JSON
                try { File.Delete(jsonPath); } catch { }
                SafeLog("[Profile] migrated " + (legacy.Units == null ? 0 : legacy.Units.Count) + " units to SQLite");
            }
            catch (Exception ex)
            {
                SafeLog("[Profile] migrate failed: " + ex);
                try { if (File.Exists(tmpDbPath)) File.Delete(tmpDbPath); } catch { }
                // 不抛 —— 让上层用空 DB 也能起来；JSON 还在，下次启动重试
            }
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            string folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                try { Directory.CreateDirectory(folder); } catch { }
            }
        }

        // ---------------------------------------------------------------------
        // read
        // ---------------------------------------------------------------------

        private void LoadBaselineInto(PersonalTypingProfile profile)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT effective_chars, speed, hit_rate, kpw, accuracy,
                                           backs_per_char, correction_per_char, waste_per_char, choose_per_char
                                    FROM baseline WHERE singleton = 1;";
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        profile.EffectiveStatCharacters = r.GetInt32(0);
                        profile.BaselineSpeed = r.GetDouble(1);
                        profile.BaselineHitRate = r.GetDouble(2);
                        profile.BaselineKpw = r.GetDouble(3);
                        profile.BaselineAccuracy = r.GetDouble(4);
                        profile.BaselineBacksPerChar = r.GetDouble(5);
                        profile.BaselineCorrectionPerChar = r.GetDouble(6);
                        profile.BaselineWasteCodesPerChar = r.GetDouble(7);
                        profile.BaselineChoosePerChar = r.GetDouble(8);
                    }
                }
            }
        }

        private void LoadCalibrationInto(PersonalTypingProfile profile)
        {
            var cal = new PersonalPredictionCalibration();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT count, observed_chars,
                                           lt_time_ratio, lt_time_weight, rec_time_ratio, rec_time_weight,
                                           lt_key_ratio, lt_key_weight, rec_key_ratio, rec_key_weight
                                    FROM calibration WHERE singleton = 1;";
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        cal.Count = r.GetInt32(0);
                        cal.ObservedCharacters = r.GetInt32(1);
                        cal.LongTermTimeRatio = r.GetDouble(2);
                        cal.LongTermTimeWeight = r.GetDouble(3);
                        cal.RecentTimeRatio = r.GetDouble(4);
                        cal.RecentTimeWeight = r.GetDouble(5);
                        cal.LongTermKeyRatio = r.GetDouble(6);
                        cal.LongTermKeyWeight = r.GetDouble(7);
                        cal.RecentKeyRatio = r.GetDouble(8);
                        cal.RecentKeyWeight = r.GetDouble(9);
                    }
                }
            }
            profile.Calibration = cal;
        }

        private Dictionary<string, PersonalTypingUnitStats> LoadUnits(IEnumerable<string> texts)
        {
            var result = new Dictionary<string, PersonalTypingUnitStats>();
            if (texts == null)
                return result;

            // 去重并跳过空串
            var batch = new List<string>();
            var seen = new HashSet<string>();
            foreach (string t in texts)
            {
                if (string.IsNullOrEmpty(t))
                    continue;
                if (seen.Add(t))
                    batch.Add(t);
            }

            if (batch.Count == 0)
                return result;

            // 分批，避免超过 SQLite 参数数量上限
            for (int chunkStart = 0; chunkStart < batch.Count; chunkStart += SQLiteMaxParameters)
            {
                int chunkSize = Math.Min(SQLiteMaxParameters, batch.Count - chunkStart);
                LoadUnitsBatch(batch, chunkStart, chunkSize, result);
            }

            return result;
        }

        private void LoadUnitsBatch(List<string> batch, int start, int count, Dictionary<string, PersonalTypingUnitStats> output)
        {
            using (var cmd = conn.CreateCommand())
            {
                var sb = new System.Text.StringBuilder();
                sb.Append(@"SELECT text, count, observed_chars, total_ms, total_keys,
                                   lt_ms, lt_keys, lt_weight, rec_ms, rec_keys, rec_weight
                            FROM units WHERE text IN (");
                for (int i = 0; i < count; i++)
                {
                    if (i > 0) sb.Append(',');
                    string p = "@p" + i;
                    sb.Append(p);
                    var param = cmd.CreateParameter();
                    param.ParameterName = p;
                    param.DbType = DbType.String;
                    param.Value = batch[start + i];
                    cmd.Parameters.Add(param);
                }
                sb.Append(");");
                cmd.CommandText = sb.ToString();

                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        var u = new PersonalTypingUnitStats
                        {
                            Text = r.GetString(0),
                            Count = r.GetInt32(1),
                            ObservedCharacters = r.GetInt32(2),
                            TotalMilliseconds = r.GetDouble(3),
                            TotalKeys = r.GetDouble(4),
                            LongTermWeightedMilliseconds = r.GetDouble(5),
                            LongTermWeightedKeys = r.GetDouble(6),
                            LongTermWeight = r.GetDouble(7),
                            RecencyWeightedMilliseconds = r.GetDouble(8),
                            RecencyWeightedKeys = r.GetDouble(9),
                            RecencyWeight = r.GetDouble(10)
                        };
                        output[u.Text] = u;
                    }
                }
            }
        }

        // ---------------------------------------------------------------------
        // write
        // ---------------------------------------------------------------------

        private static void WriteBaseline(SQLiteConnection c, SQLiteTransaction tx, PersonalTypingProfile profile)
        {
            using (var cmd = c.CreateCommand())
            {
                if (tx != null) cmd.Transaction = tx;
                cmd.CommandText = @"UPDATE baseline SET
                                        effective_chars     = @ec,
                                        speed               = @speed,
                                        hit_rate            = @hr,
                                        kpw                 = @kpw,
                                        accuracy            = @acc,
                                        backs_per_char      = @bpc,
                                        correction_per_char = @cpc,
                                        waste_per_char      = @wpc,
                                        choose_per_char     = @chpc
                                    WHERE singleton = 1;";
                AddParam(cmd, "@ec", DbType.Int32, profile.EffectiveStatCharacters);
                AddParam(cmd, "@speed", DbType.Double, profile.BaselineSpeed);
                AddParam(cmd, "@hr", DbType.Double, profile.BaselineHitRate);
                AddParam(cmd, "@kpw", DbType.Double, profile.BaselineKpw);
                AddParam(cmd, "@acc", DbType.Double, profile.BaselineAccuracy);
                AddParam(cmd, "@bpc", DbType.Double, profile.BaselineBacksPerChar);
                AddParam(cmd, "@cpc", DbType.Double, profile.BaselineCorrectionPerChar);
                AddParam(cmd, "@wpc", DbType.Double, profile.BaselineWasteCodesPerChar);
                AddParam(cmd, "@chpc", DbType.Double, profile.BaselineChoosePerChar);
                cmd.ExecuteNonQuery();
            }
        }

        private static void WriteCalibration(SQLiteConnection c, SQLiteTransaction tx, PersonalPredictionCalibration cal)
        {
            using (var cmd = c.CreateCommand())
            {
                if (tx != null) cmd.Transaction = tx;
                cmd.CommandText = @"UPDATE calibration SET
                                        count            = @count,
                                        observed_chars   = @ec,
                                        lt_time_ratio    = @ltr,
                                        lt_time_weight   = @ltw,
                                        rec_time_ratio   = @rtr,
                                        rec_time_weight  = @rtw,
                                        lt_key_ratio     = @lkr,
                                        lt_key_weight    = @lkw,
                                        rec_key_ratio    = @rkr,
                                        rec_key_weight   = @rkw
                                    WHERE singleton = 1;";
                AddParam(cmd, "@count", DbType.Int32, cal.Count);
                AddParam(cmd, "@ec", DbType.Int32, cal.ObservedCharacters);
                AddParam(cmd, "@ltr", DbType.Double, cal.LongTermTimeRatio);
                AddParam(cmd, "@ltw", DbType.Double, cal.LongTermTimeWeight);
                AddParam(cmd, "@rtr", DbType.Double, cal.RecentTimeRatio);
                AddParam(cmd, "@rtw", DbType.Double, cal.RecentTimeWeight);
                AddParam(cmd, "@lkr", DbType.Double, cal.LongTermKeyRatio);
                AddParam(cmd, "@lkw", DbType.Double, cal.LongTermKeyWeight);
                AddParam(cmd, "@rkr", DbType.Double, cal.RecentKeyRatio);
                AddParam(cmd, "@rkw", DbType.Double, cal.RecentKeyWeight);
                cmd.ExecuteNonQuery();
            }
        }

        private static void UpsertUnits(SQLiteConnection c, SQLiteTransaction tx, IEnumerable<PersonalTypingUnitStats> units)
        {
            using (var cmd = c.CreateCommand())
            {
                if (tx != null) cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO units (text, count, observed_chars, total_ms, total_keys,
                   lt_ms, lt_keys, lt_weight, rec_ms, rec_keys, rec_weight, last_seen)
VALUES (@text, @count, @ec, @tms, @tk, @ltms, @ltk, @ltw, @rms, @rk, @rw, @ls)
ON CONFLICT(text) DO UPDATE SET
  count           = excluded.count,
  observed_chars  = excluded.observed_chars,
  total_ms        = excluded.total_ms,
  total_keys      = excluded.total_keys,
  lt_ms           = excluded.lt_ms,
  lt_keys         = excluded.lt_keys,
  lt_weight       = excluded.lt_weight,
  rec_ms          = excluded.rec_ms,
  rec_keys        = excluded.rec_keys,
  rec_weight      = excluded.rec_weight,
  last_seen       = excluded.last_seen;";

                var pText = AddParam(cmd, "@text", DbType.String, "");
                var pCount = AddParam(cmd, "@count", DbType.Int32, 0);
                var pEc = AddParam(cmd, "@ec", DbType.Int32, 0);
                var pTms = AddParam(cmd, "@tms", DbType.Double, 0.0);
                var pTk = AddParam(cmd, "@tk", DbType.Double, 0.0);
                var pLtms = AddParam(cmd, "@ltms", DbType.Double, 0.0);
                var pLtk = AddParam(cmd, "@ltk", DbType.Double, 0.0);
                var pLtw = AddParam(cmd, "@ltw", DbType.Double, 0.0);
                var pRms = AddParam(cmd, "@rms", DbType.Double, 0.0);
                var pRk = AddParam(cmd, "@rk", DbType.Double, 0.0);
                var pRw = AddParam(cmd, "@rw", DbType.Double, 0.0);
                var pLs = AddParam(cmd, "@ls", DbType.Int64, 0L);

                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                foreach (var u in units)
                {
                    if (u == null || string.IsNullOrEmpty(u.Text))
                        continue;
                    pText.Value = u.Text;
                    pCount.Value = u.Count;
                    pEc.Value = u.ObservedCharacters;
                    pTms.Value = u.TotalMilliseconds;
                    pTk.Value = u.TotalKeys;
                    pLtms.Value = u.LongTermWeightedMilliseconds;
                    pLtk.Value = u.LongTermWeightedKeys;
                    pLtw.Value = u.LongTermWeight;
                    pRms.Value = u.RecencyWeightedMilliseconds;
                    pRk.Value = u.RecencyWeightedKeys;
                    pRw.Value = u.RecencyWeight;
                    pLs.Value = now;
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ---------------------------------------------------------------------
        // helpers
        // ---------------------------------------------------------------------

        private static SQLiteParameter AddParam(SQLiteCommand cmd, string name, DbType type, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.DbType = type;
            p.Value = value;
            cmd.Parameters.Add(p);
            return p;
        }

        private static void ExecuteNonQuery(SQLiteConnection c, SQLiteTransaction tx, string sql)
        {
            using (var cmd = c.CreateCommand())
            {
                if (tx != null) cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private static void SafeLog(string msg)
        {
            try { DebugLog.AppendLine(msg); }
            catch { /* DebugLog 不可用时静默吞掉 */ }
        }
    }
}
