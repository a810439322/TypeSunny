using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;

namespace TypeSunny.Personalization
{
    internal sealed class AllHistoryRoundRecord
    {
        public DateTime CreatedAt { get; set; }
        public string AppVersion { get; set; }
        public int SchemaVersion { get; set; }
        public string TargetText { get; set; }
        public string TextHash { get; set; }
        public string ArticleName { get; set; }
        public string Source { get; set; }
        public string AttemptGroupKey { get; set; }
        public int AttemptIndex { get; set; }
        public bool IsFirstAttempt { get; set; }
        public long? PreviousRoundId { get; set; }
        public string RetypeReason { get; set; }
        public int TotalWords { get; set; }
        public int InputWords { get; set; }
        public double TotalSeconds { get; set; }
        public int TotalHits { get; set; }
        public double Speed { get; set; }
        public double HitRate { get; set; }
        public double Kpw { get; set; }
        public double Accuracy { get; set; }
        public int Wrong { get; set; }
        public int Backs { get; set; }
        public double Correction { get; set; }
        public int WasteCodes { get; set; }
        public int Choose { get; set; }
        public string DifficultyText { get; set; }
        public double DifficultyScore { get; set; }
        public string[] CommitTexts { get; set; }
        public long[] CommitTimes { get; set; }
        public long[] KeyTimes { get; set; }

        public AllHistoryRoundRecord()
        {
            AppVersion = "";
            TargetText = "";
            TextHash = "";
            ArticleName = "";
            Source = "";
            AttemptGroupKey = "";
            RetypeReason = "";
            DifficultyText = "";
            CommitTexts = new string[0];
            CommitTimes = new long[0];
            KeyTimes = new long[0];
        }
    }

    internal sealed class AllHistoryUnitSample
    {
        public long RoundId { get; set; }
        public int Sequence { get; set; }
        public string UnitText { get; set; }
        public int UnitLength { get; set; }
        public int StartCharIndex { get; set; }
        public int EndCharIndex { get; set; }
        public double ElapsedMilliseconds { get; set; }
        public double KeyCount { get; set; }
        public string Source { get; set; }

        public AllHistoryUnitSample()
        {
            UnitText = "";
            Source = "commit";
        }
    }

    internal sealed class AllHistoryReplayRound
    {
        public long RoundId { get; set; }
        public string TargetText { get; set; }
        public PersonalTypingRoundStats Stats { get; set; }
        public List<AllHistoryUnitSample> Samples { get; private set; }

        public AllHistoryReplayRound()
        {
            TargetText = "";
            Stats = new PersonalTypingRoundStats();
            Samples = new List<AllHistoryUnitSample>();
        }
    }

    internal sealed class AllHistoryRoundSummary
    {
        public long RoundId { get; set; }
        public string AttemptGroupKey { get; set; }
        public int AttemptIndex { get; set; }
        public bool IsFirstAttempt { get; set; }
        public long? PreviousRoundId { get; set; }

        public AllHistoryRoundSummary()
        {
            AttemptGroupKey = "";
        }
    }

    internal sealed class AllHistoryTypingHistoryStore : IDisposable
    {
        private const int CurrentSchemaVersion = 1;
        private const long PauseThresholdMilliseconds = 10000;
        private readonly string dbPath;
        private readonly object syncRoot = new object();
        private SQLiteConnection conn;
        private bool disposed;

        public AllHistoryTypingHistoryStore()
            : this(GetDefaultDbPath())
        {
        }

        public AllHistoryTypingHistoryStore(string dbPath)
        {
            if (string.IsNullOrWhiteSpace(dbPath))
                throw new ArgumentException("dbPath is required", "dbPath");
            this.dbPath = dbPath;
        }

        public long AppendRound(AllHistoryRoundRecord record)
        {
            if (record == null)
                throw new ArgumentNullException("record");

            lock (syncRoot)
            {
                EnsureOpen();
                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        UpsertText(record, tx);
                        long roundId = InsertRound(record, tx);
                        InsertUnitSamples(roundId, record, tx);
                        tx.Commit();
                        return roundId;
                    }
                    catch
                    {
                        try { tx.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public IEnumerable<AllHistoryUnitSample> LoadUnitSamples(long roundId)
        {
            lock (syncRoot)
            {
                EnsureOpen();
                var result = new List<AllHistoryUnitSample>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT round_id, seq, unit_text, unit_length,
                                               start_char_index, end_char_index,
                                               elapsed_ms, key_count, source
                                        FROM unit_samples
                                        WHERE round_id = @round_id
                                        ORDER BY seq;";
                    AddParam(cmd, "@round_id", DbType.Int64, roundId);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                            result.Add(ReadSample(r));
                    }
                }
                return result;
            }
        }

        public AllHistoryRoundSummary LoadRoundSummary(long roundId)
        {
            lock (syncRoot)
            {
                EnsureOpen();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT round_id, attempt_group_key, attempt_index,
                                               is_first_attempt, previous_round_id
                                        FROM rounds
                                        WHERE round_id = @round_id;";
                    AddParam(cmd, "@round_id", DbType.Int64, roundId);
                    using (var r = cmd.ExecuteReader())
                    {
                        if (!r.Read())
                            return null;

                        return new AllHistoryRoundSummary
                        {
                            RoundId = r.GetInt64(0),
                            AttemptGroupKey = r.GetString(1),
                            AttemptIndex = r.GetInt32(2),
                            IsFirstAttempt = r.GetInt32(3) != 0,
                            PreviousRoundId = r.IsDBNull(4) ? (long?)null : r.GetInt64(4)
                        };
                    }
                }
            }
        }

        public IEnumerable<AllHistoryRoundSummary> LoadReplayRoundSummaries()
        {
            lock (syncRoot)
            {
                EnsureOpen();
                var result = new List<AllHistoryRoundSummary>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT round_id, attempt_group_key, attempt_index,
                                               is_first_attempt, previous_round_id
                                        FROM rounds
                                        ORDER BY created_at_unix, round_id;";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            result.Add(new AllHistoryRoundSummary
                            {
                                RoundId = r.GetInt64(0),
                                AttemptGroupKey = r.GetString(1),
                                AttemptIndex = r.GetInt32(2),
                                IsFirstAttempt = r.GetInt32(3) != 0,
                                PreviousRoundId = r.IsDBNull(4) ? (long?)null : r.GetInt64(4)
                            });
                        }
                    }
                }
                return result;
            }
        }

        public IEnumerable<AllHistoryReplayRound> LoadReplayRounds(bool firstAttemptsOnly)
        {
            lock (syncRoot)
            {
                EnsureOpen();
                var rounds = new List<AllHistoryReplayRound>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"SELECT r.round_id, t.text_content,
                                               r.total_words, r.total_seconds, r.total_hits,
                                               r.speed, r.hit_rate, r.kpw, r.accuracy,
                                               r.backs, r.correction, r.waste_codes, r.choose_count
                                        FROM rounds r
                                        JOIN texts t ON t.text_hash = r.text_hash
                                        WHERE (@first_only = 0 OR r.is_first_attempt = 1)
                                        ORDER BY r.created_at_unix, r.round_id;";
                    AddParam(cmd, "@first_only", DbType.Int32, firstAttemptsOnly ? 1 : 0);
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            rounds.Add(new AllHistoryReplayRound
                            {
                                RoundId = r.GetInt64(0),
                                TargetText = r.GetString(1),
                                Stats = new PersonalTypingRoundStats
                                {
                                    TotalWords = r.GetInt32(2),
                                    TotalSeconds = r.GetDouble(3),
                                    TotalHits = r.GetInt32(4),
                                    Speed = r.GetDouble(5),
                                    HitRate = r.GetDouble(6),
                                    Kpw = r.GetDouble(7),
                                    Accuracy = r.GetDouble(8),
                                    Backs = r.GetInt32(9),
                                    Correction = r.GetDouble(10),
                                    WasteCodes = r.GetInt32(11),
                                    Choose = r.GetInt32(12)
                                }
                            });
                        }
                    }
                }

                foreach (var round in rounds)
                    round.Samples.AddRange(LoadUnitSamplesUnlocked(round.RoundId));

                return rounds;
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

        private List<AllHistoryUnitSample> LoadUnitSamplesUnlocked(long roundId)
        {
            var result = new List<AllHistoryUnitSample>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"SELECT round_id, seq, unit_text, unit_length,
                                           start_char_index, end_char_index,
                                           elapsed_ms, key_count, source
                                    FROM unit_samples
                                    WHERE round_id = @round_id
                                    ORDER BY seq;";
                AddParam(cmd, "@round_id", DbType.Int64, roundId);
                using (var r = cmd.ExecuteReader())
                {
                    while (r.Read())
                        result.Add(ReadSample(r));
                }
            }
            return result;
        }

        private static AllHistoryUnitSample ReadSample(SQLiteDataReader r)
        {
            return new AllHistoryUnitSample
            {
                RoundId = r.GetInt64(0),
                Sequence = r.GetInt32(1),
                UnitText = r.GetString(2),
                UnitLength = r.GetInt32(3),
                StartCharIndex = r.GetInt32(4),
                EndCharIndex = r.GetInt32(5),
                ElapsedMilliseconds = r.GetDouble(6),
                KeyCount = r.GetDouble(7),
                Source = r.GetString(8)
            };
        }

        private void UpsertText(AllHistoryRoundRecord record, SQLiteTransaction tx)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT OR IGNORE INTO texts
                                      (text_hash, text_content, char_count, first_seen_unix)
                                    VALUES (@hash, @content, @chars, @seen);";
                AddParam(cmd, "@hash", DbType.String, NormalizeHash(record));
                AddParam(cmd, "@content", DbType.String, record.TargetText ?? "");
                AddParam(cmd, "@chars", DbType.Int32, CountTextElements(record.TargetText));
                AddParam(cmd, "@seen", DbType.Int64, ToUnixSeconds(record.CreatedAt));
                cmd.ExecuteNonQuery();
            }
        }

        private long InsertRound(AllHistoryRoundRecord record, SQLiteTransaction tx)
        {
            string groupKey = string.IsNullOrEmpty(record.AttemptGroupKey) ? NormalizeHash(record) : record.AttemptGroupKey;
            AttemptInfo attempt = ResolveAttemptInfo(groupKey, record, tx);

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO rounds
                    (created_at_unix, app_version, schema_version, text_hash,
                     article_name, source, attempt_group_key, attempt_index,
                     is_first_attempt, previous_round_id, retype_reason,
                     total_words, input_words, total_seconds, total_hits,
                     speed, hit_rate, kpw, accuracy, wrong, backs, correction,
                     waste_codes, choose_count, difficulty_text, difficulty_score)
                    VALUES
                    (@created, @app, @schema, @hash,
                     @article, @source, @group_key, @attempt_index,
                     @is_first, @previous_round_id, @retype_reason,
                     @total_words, @input_words, @total_seconds, @total_hits,
                     @speed, @hit_rate, @kpw, @accuracy, @wrong, @backs, @correction,
                     @waste_codes, @choose_count, @difficulty_text, @difficulty_score);
                    SELECT last_insert_rowid();";
                AddParam(cmd, "@created", DbType.Int64, ToUnixSeconds(record.CreatedAt));
                AddParam(cmd, "@app", DbType.String, record.AppVersion ?? "");
                AddParam(cmd, "@schema", DbType.Int32, record.SchemaVersion > 0 ? record.SchemaVersion : CurrentSchemaVersion);
                AddParam(cmd, "@hash", DbType.String, NormalizeHash(record));
                AddParam(cmd, "@article", DbType.String, record.ArticleName ?? "");
                AddParam(cmd, "@source", DbType.String, record.Source ?? "");
                AddParam(cmd, "@group_key", DbType.String, groupKey);
                AddParam(cmd, "@attempt_index", DbType.Int32, attempt.AttemptIndex);
                AddParam(cmd, "@is_first", DbType.Int32, attempt.IsFirstAttempt ? 1 : 0);
                AddParam(cmd, "@previous_round_id", DbType.Int64, attempt.PreviousRoundId.HasValue ? (object)attempt.PreviousRoundId.Value : DBNull.Value);
                AddParam(cmd, "@retype_reason", DbType.String, record.RetypeReason ?? "");
                AddParam(cmd, "@total_words", DbType.Int32, record.TotalWords);
                AddParam(cmd, "@input_words", DbType.Int32, record.InputWords);
                AddParam(cmd, "@total_seconds", DbType.Double, record.TotalSeconds);
                AddParam(cmd, "@total_hits", DbType.Int32, record.TotalHits);
                AddParam(cmd, "@speed", DbType.Double, record.Speed);
                AddParam(cmd, "@hit_rate", DbType.Double, record.HitRate);
                AddParam(cmd, "@kpw", DbType.Double, record.Kpw);
                AddParam(cmd, "@accuracy", DbType.Double, record.Accuracy);
                AddParam(cmd, "@wrong", DbType.Int32, record.Wrong);
                AddParam(cmd, "@backs", DbType.Int32, record.Backs);
                AddParam(cmd, "@correction", DbType.Double, record.Correction);
                AddParam(cmd, "@waste_codes", DbType.Int32, record.WasteCodes);
                AddParam(cmd, "@choose_count", DbType.Int32, record.Choose);
                AddParam(cmd, "@difficulty_text", DbType.String, record.DifficultyText ?? "");
                AddParam(cmd, "@difficulty_score", DbType.Double, record.DifficultyScore);
                return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            }
        }

        private sealed class AttemptInfo
        {
            public int AttemptIndex;
            public bool IsFirstAttempt;
            public long? PreviousRoundId;
        }

        private AttemptInfo ResolveAttemptInfo(string groupKey, AllHistoryRoundRecord record, SQLiteTransaction tx)
        {
            long? previousRoundId = null;
            int previousMax = 0;
            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"SELECT round_id, attempt_index
                                    FROM rounds
                                    WHERE attempt_group_key = @group_key
                                    ORDER BY attempt_index DESC, round_id DESC
                                    LIMIT 1;";
                AddParam(cmd, "@group_key", DbType.String, groupKey);
                using (var r = cmd.ExecuteReader())
                {
                    if (r.Read())
                    {
                        previousRoundId = r.GetInt64(0);
                        previousMax = r.GetInt32(1);
                    }
                }
            }

            int attemptIndex = record.AttemptIndex > 0 ? record.AttemptIndex : previousMax + 1;
            if (attemptIndex <= 0)
                attemptIndex = 1;

            return new AttemptInfo
            {
                AttemptIndex = attemptIndex,
                IsFirstAttempt = attemptIndex == 1 && record.IsFirstAttempt,
                PreviousRoundId = record.PreviousRoundId.HasValue ? record.PreviousRoundId : previousRoundId
            };
        }

        private void InsertUnitSamples(long roundId, AllHistoryRoundRecord record, SQLiteTransaction tx)
        {
            string[] commits = record.CommitTexts ?? new string[0];
            long[] commitTimes = record.CommitTimes ?? new long[0];
            long[] keyTimes = (record.KeyTimes ?? new long[0]).OrderBy(t => t).ToArray();
            int count = Math.Min(commits.Length, commitTimes.Length);
            int charIndex = 0;
            long previousCommitTime = 0;

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO unit_samples
                    (round_id, seq, unit_text, unit_length, start_char_index, end_char_index,
                     elapsed_ms, key_count, source)
                    VALUES
                    (@round_id, @seq, @unit_text, @unit_length, @start_char_index, @end_char_index,
                     @elapsed_ms, @key_count, @source);";
                var pRound = AddParam(cmd, "@round_id", DbType.Int64, roundId);
                var pSeq = AddParam(cmd, "@seq", DbType.Int32, 0);
                var pUnit = AddParam(cmd, "@unit_text", DbType.String, "");
                var pLen = AddParam(cmd, "@unit_length", DbType.Int32, 0);
                var pStart = AddParam(cmd, "@start_char_index", DbType.Int32, 0);
                var pEnd = AddParam(cmd, "@end_char_index", DbType.Int32, 0);
                var pMs = AddParam(cmd, "@elapsed_ms", DbType.Double, 0.0);
                var pKeys = AddParam(cmd, "@key_count", DbType.Double, 0.0);
                var pSource = AddParam(cmd, "@source", DbType.String, "commit");

                for (int i = 0; i < count; i++)
                {
                    string unit = commits[i] ?? "";
                    int unitLength = CountTextElements(unit);
                    long commitTime = commitTimes[i];
                    if (unitLength <= 0)
                    {
                        previousCommitTime = commitTime;
                        continue;
                    }

                    pRound.Value = roundId;
                    pSeq.Value = i + 1;
                    pUnit.Value = unit;
                    pLen.Value = unitLength;
                    pStart.Value = charIndex;
                    pEnd.Value = charIndex + unitLength;
                    pMs.Value = EffectiveMillisecondsBetween(previousCommitTime, commitTime, keyTimes);
                    pKeys.Value = CountKeysBetween(previousCommitTime, commitTime, keyTimes);
                    pSource.Value = "commit";
                    cmd.ExecuteNonQuery();

                    charIndex += unitLength;
                    previousCommitTime = commitTime;
                }
            }
        }

        private void EnsureOpen()
        {
            if (disposed)
                throw new ObjectDisposedException("AllHistoryTypingHistoryStore");
            if (conn != null)
                return;

            EnsureDirectoryExists(dbPath);
            conn = new SQLiteConnection("Data Source=" + dbPath + ";Version=3;Pooling=False;");
            conn.Open();
            ExecuteNonQuery("PRAGMA journal_mode = WAL;");
            ExecuteNonQuery("PRAGMA synchronous = NORMAL;");
            ExecuteNonQuery("PRAGMA temp_store = MEMORY;");
            EnsureSchema();
        }

        private void EnsureSchema()
        {
            ExecuteNonQuery(@"
CREATE TABLE IF NOT EXISTS meta (
  key TEXT PRIMARY KEY,
  value TEXT
);
INSERT OR IGNORE INTO meta (key, value) VALUES ('schema_version', '1');

CREATE TABLE IF NOT EXISTS texts (
  text_hash TEXT PRIMARY KEY,
  text_content TEXT NOT NULL,
  char_count INTEGER NOT NULL,
  first_seen_unix INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS rounds (
  round_id INTEGER PRIMARY KEY AUTOINCREMENT,
  created_at_unix INTEGER NOT NULL,
  app_version TEXT NOT NULL,
  schema_version INTEGER NOT NULL,
  text_hash TEXT NOT NULL,
  article_name TEXT,
  source TEXT,
  attempt_group_key TEXT NOT NULL,
  attempt_index INTEGER NOT NULL,
  is_first_attempt INTEGER NOT NULL,
  previous_round_id INTEGER,
  retype_reason TEXT,
  total_words INTEGER NOT NULL,
  input_words INTEGER NOT NULL,
  total_seconds REAL NOT NULL,
  total_hits INTEGER NOT NULL,
  speed REAL NOT NULL,
  hit_rate REAL NOT NULL,
  kpw REAL NOT NULL,
  accuracy REAL NOT NULL,
  wrong INTEGER NOT NULL,
  backs INTEGER NOT NULL,
  correction REAL NOT NULL,
  waste_codes INTEGER NOT NULL,
  choose_count INTEGER NOT NULL,
  difficulty_text TEXT,
  difficulty_score REAL,
  FOREIGN KEY(text_hash) REFERENCES texts(text_hash)
);

CREATE TABLE IF NOT EXISTS unit_samples (
  sample_id INTEGER PRIMARY KEY AUTOINCREMENT,
  round_id INTEGER NOT NULL,
  seq INTEGER NOT NULL,
  unit_text TEXT NOT NULL,
  unit_length INTEGER NOT NULL,
  start_char_index INTEGER NOT NULL,
  end_char_index INTEGER NOT NULL,
  elapsed_ms REAL NOT NULL,
  key_count REAL NOT NULL,
  source TEXT NOT NULL,
  FOREIGN KEY(round_id) REFERENCES rounds(round_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_rounds_created ON rounds(created_at_unix, round_id);
CREATE INDEX IF NOT EXISTS idx_rounds_attempt ON rounds(is_first_attempt, attempt_index);
CREATE INDEX IF NOT EXISTS idx_rounds_text_hash ON rounds(text_hash);
CREATE INDEX IF NOT EXISTS idx_unit_samples_round ON unit_samples(round_id, seq);
CREATE INDEX IF NOT EXISTS idx_unit_samples_unit ON unit_samples(unit_text);
");
        }

        private void ExecuteNonQuery(string sql)
        {
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private static SQLiteParameter AddParam(SQLiteCommand cmd, string name, DbType type, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.DbType = type;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
            return p;
        }

        private static int CountKeysBetween(long startExclusive, long endInclusive, long[] keyTimes)
        {
            int count = 0;
            for (int i = 0; i < keyTimes.Length; i++)
            {
                if (keyTimes[i] > startExclusive && keyTimes[i] <= endInclusive)
                    count++;
            }
            return count;
        }

        private static double EffectiveMillisecondsBetween(long start, long end, long[] keyTimes)
        {
            if (end <= start)
                return 0;

            var events = new List<long>();
            events.Add(start);
            for (int i = 0; i < keyTimes.Length; i++)
            {
                if (keyTimes[i] > start && keyTimes[i] < end)
                    events.Add(keyTimes[i]);
            }
            events.Add(end);

            double effective = end - start;
            for (int i = 1; i < events.Count; i++)
            {
                long gap = events[i] - events[i - 1];
                if (gap > PauseThresholdMilliseconds)
                    effective -= gap;
            }

            return Math.Max(0, effective);
        }

        private static string NormalizeHash(AllHistoryRoundRecord record)
        {
            if (!string.IsNullOrEmpty(record.TextHash))
                return record.TextHash;
            return PersonalScorePredictionSnapshot.ComputeTextHash(record.TargetText);
        }

        private static int CountTextElements(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            return new StringInfo(text).LengthInTextElements;
        }

        private static long ToUnixSeconds(DateTime value)
        {
            DateTime normalized = value == default(DateTime) ? DateTime.Now : value;
            return new DateTimeOffset(normalized).ToUnixTimeSeconds();
        }

        private static string GetDefaultDbPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "预测日志", "all_history.db");
        }

        private static void EnsureDirectoryExists(string filePath)
        {
            string folder = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                try { Directory.CreateDirectory(folder); } catch { }
            }
        }
    }
}
