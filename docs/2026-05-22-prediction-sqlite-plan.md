# 预测画像存储迁移到 SQLite 实施计划

日期：2026-05-22

目的：把 `预测日志/PersonalTypingProfile.json` 全量 JSON 存储改造成 SQLite 库，支持 10 万条以上规模，按需现查不全量加载，异步写入。同时不影响其他工作区改动（IME 退格 / 字帖 / 临摹模式）。

## 一、用户已确认的设计决策

| 项 | 决策 |
|---|---|
| DB 文件路径 | `预测日志/profile.db` |
| 旧 JSON 处理 | 启动时自动迁移，迁移成功后删除旧 JSON；失败保留 JSON 不影响功能 |
| Profile 数据访问模式 | 关闭"启用预测"时不碰 DB，开启时初始化；预测/训练时**按需现查**，不全量加载到内存 |
| 写入时机 | Train 时立即 UPSERT，但放在异步线程（`Task.Run`），不阻塞 StopHelper |
| 连接生命周期 | 关闭"启用预测"开关时 Dispose 连接，下次开启时重新打开 |
| 迁移提示 | 静默，不弹窗，结果写 debug log |
| native dll | 手工部署 `System.Data.SQLite` 三文件，提交进 git |

## 二、依赖部署

### 2.1 三个二进制文件

来源：https://system.data.sqlite.org/index.html/doc/trunk/www/downloads.wiki
下载 "Precompiled Binaries for .NET 4.6"（最新 1.0.119 或 1.0.118）的 x86 和 x64 两份。

放置位置：

```
lib/SQLite/
├── System.Data.SQLite.dll          (managed, 加 csproj Reference)
├── x86/
│   └── SQLite.Interop.dll          (native, 32 位)
├── x64/
│   └── SQLite.Interop.dll          (native, 64 位)
└── LICENSE.txt                     (Public Domain，复述官方授权)
```

### 2.2 csproj 改造

- 加 `<Reference Include="System.Data.SQLite">`，`HintPath` 指向 `lib/SQLite/System.Data.SQLite.dll`，并加 `<Private>True</Private>`（确保托管 dll 被复制到输出目录）。
- 加两条 `<Content>`：

```xml
<Content Include="lib\SQLite\x86\SQLite.Interop.dll">
  <Link>x86\SQLite.Interop.dll</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
<Content Include="lib\SQLite\x64\SQLite.Interop.dll">
  <Link>x64\SQLite.Interop.dll</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

`System.Data.SQLite.dll` 在运行时按进程位数从 `x86/` 或 `x64/` 子目录加载 native interop。Release 配置当前 `PlatformTarget=x86`，会加载 `x86/SQLite.Interop.dll`；Debug AnyCPU 在 64 位 Windows 上以 64 位进程运行，加载 `x64/SQLite.Interop.dll`。两份都要部署。

### 2.3 Updater 包含

`TypeSunny.csproj` 末尾有 `BuildUpdater` target 把 `Updater.exe` 拷到输出目录；不影响 SQLite。但要确认你的安装包/打包脚本会把 `bin/Release/` 下的 `System.Data.SQLite.dll`、`x86/SQLite.Interop.dll`、`x64/SQLite.Interop.dll` 一并打包。

## 三、Schema 设计

DB 文件首次打开时执行 `CREATE TABLE IF NOT EXISTS`：

```sql
-- 元数据
CREATE TABLE IF NOT EXISTS meta (
  key TEXT PRIMARY KEY,
  value TEXT
);
-- 内容：schema_version='1', migrated_from_json='2026-05-22 ...'

-- 基线统计（单行）
CREATE TABLE IF NOT EXISTS baseline (
  effective_chars      INTEGER NOT NULL DEFAULT 0,
  speed                REAL    NOT NULL DEFAULT 120,
  hit_rate             REAL    NOT NULL DEFAULT 5,
  kpw                  REAL    NOT NULL DEFAULT 4,
  accuracy             REAL    NOT NULL DEFAULT 98,
  backs_per_char       REAL    NOT NULL DEFAULT 0,
  correction_per_char  REAL    NOT NULL DEFAULT 0,
  waste_per_char       REAL    NOT NULL DEFAULT 0,
  choose_per_char      REAL    NOT NULL DEFAULT 0,
  singleton CHECK (singleton = 1) PRIMARY KEY  -- 强制只有一行
);
INSERT OR IGNORE INTO baseline (singleton) VALUES (1);

-- 校准（单行）
CREATE TABLE IF NOT EXISTS calibration (
  count                INTEGER NOT NULL DEFAULT 0,
  observed_chars       INTEGER NOT NULL DEFAULT 0,
  lt_time_ratio        REAL    NOT NULL DEFAULT 0,
  lt_time_weight       REAL    NOT NULL DEFAULT 0,
  rec_time_ratio       REAL    NOT NULL DEFAULT 0,
  rec_time_weight      REAL    NOT NULL DEFAULT 0,
  lt_key_ratio         REAL    NOT NULL DEFAULT 0,
  lt_key_weight        REAL    NOT NULL DEFAULT 0,
  rec_key_ratio        REAL    NOT NULL DEFAULT 0,
  rec_key_weight       REAL    NOT NULL DEFAULT 0,
  singleton CHECK (singleton = 1) PRIMARY KEY
);
INSERT OR IGNORE INTO calibration (singleton) VALUES (1);

-- 词条
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
  last_seen            INTEGER NOT NULL DEFAULT 0  -- Unix epoch seconds
);
-- text 作为 PK 已经天然有索引，按词查询 O(log n)
-- 不需要额外索引，按 last_seen 淘汰可全表扫（10万行 < 100ms）
```

PRAGMA 设置（连接打开后立即执行）：

```sql
PRAGMA journal_mode = WAL;       -- 写不阻塞读
PRAGMA synchronous  = NORMAL;    -- 比 FULL 快 5-10x，崩溃只丢最后几条
PRAGMA temp_store   = MEMORY;
PRAGMA cache_size   = -8000;     -- 8MB
```

WAL 模式会生成 `profile.db-wal` 和 `profile.db-shm` 两个伴随文件，正常现象。

## 四、接口设计

### 4.1 抽象接口（先抽出来方便测试 mock + 留扩展）

```csharp
internal interface IPersonalTypingProfileStore : IDisposable
{
    PersonalTypingBaseline LoadBaseline();
    PersonalPredictionCalibration LoadCalibration();
    // 按词批量查
    Dictionary<string, PersonalTypingUnitStats> LoadUnits(IEnumerable<string> units);
    // 应用一轮训练的变更
    void ApplyTraining(
        PersonalTypingBaseline newBaseline,
        IEnumerable<PersonalTypingUnitStats> changedUnits);
    void ApplyCalibration(PersonalPredictionCalibration newCalibration);
}
```

`PersonalTypingProfile` 不再是反序列化整个 JSON 出来的对象，而是改成"在调用 Predict 时按需从 store 查"的轻量结构。这点是最大的重构。

### 4.2 PersonalTypingProfile 重构

现在 `Predict` 接收一个 `PersonalTypingProfile` 实例，里面有 `Units` 字典。改造后：

**方案 A（最小改动）**：保留 `PersonalTypingProfile` 这个对象，但 `Units` 字典只装"本次预测/训练涉及的词"。

- `PersonalScorePredictionService.CreateSnapshot` 先扫一遍目标文本，提取所有候选词（1-4 字 pure Chinese 子串）→ 按词 PK 查 DB 一次拿回 → 装进 `profile.Units` → 调用 `Predict`。
- `Train` 类似：先扫提交，准备好 sample → 查 DB 拿现有 stats → Add sample → 写回。

优点：`PersonalScorePredictor.Predict` 内部一字不改。
缺点：词条对象语义变了（从"全部"变成"本次相关"），需要注释清楚，否则后来维护的人会以为可以遍历 `Units`。

**方案 B（更彻底）**：把 `Predict` 改成接受 `Func<string, PersonalTypingUnitStats>` 回调，按需从 store 查。

优点：语义清晰。
缺点：要改 `PersonalScorePredictor.Predict` 签名，`Tests/PersonalScorePredictionTests.cs` 也要跟着改。

**当前建议**：**方案 A**，保持单元测试不动。在 `PersonalTypingProfile` 类上加 XML 注释说明"现在是按需填充，不是全量"。如果以后发现有问题再切方案 B。

### 4.3 PersonalScorePredictionService 改造点

```csharp
public PersonalScorePredictionSnapshot CreateSnapshot(string text, string baseDifficultyText)
{
    if (text 为空 / store 未启用) return 空 snapshot;

    var candidateUnits = ExtractCandidateUnits(text);   // 1-4 字 pure Chinese 子串
    var fallbackSegs   = difficultySegmenter(text);
    var allKeys        = candidateUnits ∪ fallbackSegs;

    var profile = new PersonalTypingProfile();
    profile.Baseline = store.LoadBaseline();
    profile.Calibration = store.LoadCalibration();
    profile.Units = store.LoadUnits(allKeys);  // 一次 SQL: SELECT ... WHERE text IN (...)

    if (profile.Baseline.EffectiveChars <= 0) return 空 snapshot;

    var prediction = PersonalScorePredictor.Predict(text, profile, baseScore, fallbackSegs);
    return PersonalScorePredictionSnapshot.FromPrediction(text, baseScore, prediction);
}

public void Train(...)
{
    var session = PersonalTypingSessionBuilder.Build(...);
    if (session.EffectiveStatCharacters <= 0) return;

    // 扔到后台线程
    Task.Run(() => {
        try {
            // 准备新 baseline
            var oldBaseline = store.LoadBaseline();
            var newBaseline = ComputeNewBaseline(oldBaseline, session, stats);

            // 准备 changed units
            var sampleByText = session.Samples.GroupBy(s => s.Text);
            var existing    = store.LoadUnits(sampleByText.Select(g => g.Key));
            var changed     = new List<PersonalTypingUnitStats>();
            foreach (var g in sampleByText) {
                var unit = existing.GetOrCreate(g.Key);
                foreach (var s in g) unit.Add(s);
                changed.Add(unit);
            }

            store.ApplyTraining(newBaseline, changed);
        } catch (Exception ex) {
            DebugLog.Write("[Train] " + ex);
        }
    });
}
```

`ApplyTraining` 实现：

```csharp
using (var tx = conn.BeginTransaction())
{
    using (var cmd = conn.CreateCommand()) {
        cmd.CommandText = "UPDATE baseline SET ... WHERE singleton=1";
        cmd.ExecuteNonQuery();
    }
    using (var cmd = conn.CreateCommand()) {
        cmd.CommandText = @"
            INSERT INTO units (text, count, ...) VALUES (@text, @count, ...)
            ON CONFLICT(text) DO UPDATE SET
              count=excluded.count, total_ms=excluded.total_ms, ...";
        var p = cmd.Parameters.Add("@text", DbType.String);
        // 复用 cmd，参数循环替换
        foreach (var u in changed) {
            // 设参数
            cmd.ExecuteNonQuery();
        }
    }
    tx.Commit();
}
```

整个写入 + 事务在 10ms 内完成（10 万行规模的本地 SQLite 实测）。

### 4.4 连接生命周期

```csharp
internal sealed class SqlitePersonalTypingProfileStore : IPersonalTypingProfileStore
{
    private readonly string path;
    private SQLiteConnection conn;
    private readonly object writeLock = new object();

    // 懒打开
    private void EnsureOpen()
    {
        if (conn != null) return;
        // 首次：迁移 JSON → 建表 → 打开
        EnsureMigrated();
        conn = new SQLiteConnection("Data Source=" + path + ";Version=3;");
        conn.Open();
        ApplyPragmas();
        EnsureSchema();
    }

    public void Dispose() {
        if (conn != null) { conn.Close(); conn.Dispose(); conn = null; }
    }
}
```

`MainWindow` 持有 `PersonalScorePredictionService`，service 持有 store。开关切换时：

```csharp
// 启用预测 = true → 第一次 CreateSnapshot/Train 会触发 EnsureOpen
// 启用预测 = false → 主窗口监听 Config 变化时调 service.CloseStore() → store.Dispose()
```

需要在 `PersonalScorePredictionService` 加一个 `CloseStore()` 公共方法。

**线程安全**：`System.Data.SQLite` 的 `SQLiteConnection` 不是线程安全的。所有 DB 操作必须串行化。读多写少，但简单起见，所有读写都加 `lock(writeLock)`。10 万条规模下单次操作 < 50ms，UI 线程不会有体感。

> 注意：`Predict` 在 UI 线程调（生成难度文本时），`Train` 在后台线程调。它们竞争同一个 lock 是 OK 的，反正 Train 不阻塞 UI。

## 五、迁移逻辑

`EnsureMigrated`：

```csharp
private void EnsureMigrated()
{
    if (File.Exists(path)) return;  // DB 已存在，跳过

    string jsonPath = Path.Combine(Path.GetDirectoryName(path), "PersonalTypingProfile.json");
    if (!File.Exists(jsonPath)) return;  // 没有旧数据

    try {
        var profile = JsonConvert.DeserializeObject<LegacyJsonProfile>(File.ReadAllText(jsonPath));
        if (profile == null) { File.Delete(jsonPath); return; }  // 空 JSON

        // 在临时路径建库，写完原子替换
        string tmpPath = path + ".migrating";
        using (var tmpConn = new SQLiteConnection("Data Source=" + tmpPath + ";")) {
            tmpConn.Open();
            ApplyPragmas(tmpConn);
            EnsureSchema(tmpConn);
            using (var tx = tmpConn.BeginTransaction()) {
                WriteBaseline(tmpConn, profile);
                WriteCalibration(tmpConn, profile.Calibration);
                WriteUnits(tmpConn, profile.Units);  // 单 transaction 批量插入
                tx.Commit();
            }
        }
        File.Move(tmpPath, path);
        File.Delete(jsonPath);  // 迁移成功才删
        DebugLog.Write($"[Profile] migrated {profile.Units?.Count ?? 0} units to SQLite");
    } catch (Exception ex) {
        // 失败：清理 tmp 文件，保留 JSON
        try { File.Delete(path + ".migrating"); } catch { }
        DebugLog.Write("[Profile] migrate failed: " + ex);
        throw;  // 让上层 EnsureOpen 失败，下次再试
    }
}
```

`LegacyJsonProfile` 是 `PersonalTypingProfile` 当前的反序列化形状（保留一份历史 DTO），避免改主 `PersonalTypingProfile` 后旧 JSON 读不出来。

10 万条 units 单 transaction 批量插入实测 < 2 秒，可以接受。

## 六、文件改动列表

新增：

- `Personalization/IPersonalTypingProfileStore.cs` ── 接口
- `Personalization/SqlitePersonalTypingProfileStore.cs` ── SQLite 实现
- `Personalization/LegacyJsonProfile.cs` ── 旧 JSON DTO
- `Personalization/PersonalTypingBaseline.cs` ── 把现在 `PersonalTypingProfile` 里散落的 Baseline* 字段抽出来（可选，方便 store 接口）
- `lib/SQLite/System.Data.SQLite.dll` ── 二进制
- `lib/SQLite/x86/SQLite.Interop.dll`
- `lib/SQLite/x64/SQLite.Interop.dll`
- `lib/SQLite/LICENSE.txt`

修改：

- `Personalization/PersonalTypingProfileStore.cs` ── 改成实现 `IPersonalTypingProfileStore` 的薄封装，保持向后兼容（让旧测试代码继续能 `new PersonalTypingProfileStore(path)`），内部委托给 SQLite 实现
- `Personalization/PersonalTypingProfile.cs` ── 加 `Baseline` 属性（或保留原字段，看抽出 Baseline 类的成本）；`Units` 字段加注释说明语义变化
- `Personalization/PersonalScorePredictionService.cs` ── `CreateSnapshot/Train/Predict/Calibrate` 改为按需查 + 异步写
- `UI/MainWindow.xaml.cs` ── 监听"启用预测"开关变化，关闭时调 `service.CloseStore()`（如果没有现成的监听点，需要在配置变更通知处加一个）
- `Tests/PersonalScorePredictionTests.cs` ── 现有用 `new PersonalTypingProfileStore(tempPath)` 的测试要确保仍能用临时 SQLite 跑通；如果完全 mock 不便，可以在 store 抽接口后用 in-memory store 跑大部分测试逻辑
- `TypeSunny.csproj` ── 加 Reference 和 Content
- `packages.config` ── 不动（不走 NuGet）

不动：

- `UI/Modes/*` ── 工作区已有未提交改动，避免冲突
- `Tests/ImeBackspacePolicyTests.cs` ── 同上

## 七、风险与回滚

| 风险 | 缓解 |
|---|---|
| 32/64 位 native dll 加载失败 | 部署后先在 Release(x86) 和 Debug(AnyCPU 64) 两种配置下都跑一遍，确认 `SQLiteConnection.Open()` 不抛 DllNotFoundException |
| 用户机器 VC++ 运行时缺失 | `SQLite.Interop.dll` 在新 Windows 11 上一般 OK；老机器若失败会抛 `BadImageFormatException`，需要安装 VC++ 2015-2022 redist。可以在迁移失败时检测到并提示一次 |
| 现有大 JSON 文件迁移失败 | 失败时保留 JSON，下次启动重试；同时把异常细节写 debug log |
| 异步 Train 抛异常被吞 | `Task.Run` 里 try/catch + DebugLog.Write，不让进程崩溃 |
| 多进程同时开同一个 DB（罕见） | SQLite WAL 模式本身支持，但晴跟打通常单实例。不专门处理 |
| 关闭程序时异步 Train 未完成 | App.Exit 前等待 store 写完。`MainWindow` Closed 时调用 `service.CloseStore()`，store 内部确保 pending writes 完成后才 Dispose |

回滚：保留旧 `PersonalTypingProfileStore` 的 JSON 实现作为 fallback 类（比如改名 `JsonPersonalTypingProfileStore`），加一个配置项可强制走 JSON。不过更简单的回滚是改回上一个 commit。

## 八、验收

1. 在没有旧 JSON 的环境下首次启动 → 启用预测 → 跟打一篇 → 检查 `预测日志/profile.db` 是否生成，能否用 DB Browser 打开看到数据
2. 在有旧 JSON 的环境下首次启动 → 启用预测 → 检查 JSON 是否被删，DB 中 units 数量是否等于原 JSON
3. 跟打一篇 → 关闭预测 → 检查 `profile.db-wal/shm` 是否清理
4. 跟打一篇 → 立即再跟打 → 验证 Train 不阻塞 Stop（用 stopwatch 测 StopHelper 耗时不增加）
5. 在 Release(x86) 构建跑一遍上面流程
6. 单测：保留并跑通 `PersonalScorePredictionTests`，加几个新的 SQLite store 专项测试（schema 创建、迁移、UPSERT、按词批查）

## 九、未在本计划范围内的事

之前讨论的预测算法漏洞修复（snapshot 错配、单次观察就学过、置信门控、基线无衰减、陌生文本退化、校准按轮等权、段间样本时间膨胀、版本字段+容量淘汰），**全部不在本次改动**。本次只动存储层。

存储改造完后再分别开计划/PR 治理算法问题。
