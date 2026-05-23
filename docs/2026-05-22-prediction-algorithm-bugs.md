# 预测算法 BUG 清单（第二波改造依据）

日期：2026-05-22

来源：在调研 SQLite 存储改造时一并梳理了算法层的问题。本文不写实现，只描述每个问题、定位、影响、修法。等 SQLite 改造完成、行为与现版完全一致后再分批治理。

---

## 一、算法链路简述

**第 1 层 `QingDifficultyScorer`（基础难度）**：纯文本属性，DP 切词最小化总码长，单字按 `min(10, freq^1.5/100000)` 加 Hard，多字按 `2000/(freq+2000)` 加 Water，最终 `Score = Hard/Water`。

**第 2 层 `PersonalScorePredictor`（个人预测）**：第二个 DP，目标变成最小化预测耗时。候选单元 1-4 字；多字必须是用户学过或难度切词器给的 fallback 段。学过用词条平均代价；未学过按 `charCount*60000/BaselineSpeed`。DP 输出秒数和键数后乘 `Calibration.TimeFactor/KeyFactor`。`PersonalDifficultyScore = baseDifficulty × baselineSpeed / predictedSpeed`，`Confidence = MatureChars / TotalChars`。

**第 3 层 `PersonalTypingProfile`**：训练时把每轮提交切样本（commit + difficultySegmenter 给的额外段），更新 Baseline*（字符数加权平均）和 Units[text]（双轨指数衰减 0.98/0.90，65/35 加权）。

**第 4 层 `PersonalPredictionCalibration`**：用 `actualSeconds/predictedSeconds` 和 `actualHits/predictedHits` 的指数移动平均当全局乘法因子。

---

## 二、Bug 清单（按严重程度）

### P0 - 真实 bug，影响行为正确性

#### BUG-A：snapshot 错配，校准用错快照

**定位**：`MainWindow.xaml.cs:4749` Calibrate 调用 + `MainWindow.xaml.cs:189/2604/2616/6376` snapshot 字段被覆盖。

**现象**：
- `currentPersonalPredictionSnapshot` 是 `MainWindow` 的字段，任何文本/难度刷新都覆盖。
- StopHelper 触发 Calibrate 时，如果中间发生过文章切换、预览刷新、`Dispatcher.BeginInvoke` 难度回调，snapshot 已经不是 StopHelper 那轮真实跟打对应的 snapshot。
- 极端场景：用户打完 A 文 → 切到 B 文（snapshot 被覆盖）→ A 文的 StopHelper 这才执行 → Calibrate 把 B 文的 PredictedSeconds 与 A 文的 actualSeconds 配对。
- 即使没切文章，难度面板刷新会重算 snapshot，PredictedSeconds 受 TimeFactor 影响微变 → 用"被 TimeFactor 修过的 PredictedSeconds"再去算新的 ratio → **校准因子自激震荡**。

**影响**：TimeFactor/KeyFactor 长期漂移，校准越校越偏。`ClampRatio [0.25, 4.0]` 只能限制单次极端值，无法阻止系统性漂移。

**修法**：
1. 给 `PersonalScorePredictionSnapshot` 加 `TargetTextHash`（SHA1 前 8 字节足够），`Calibrate` 校验 actual 对应的文本 hash 与 snapshot 一致才更新。
2. snapshot 拍照后到消费前不要覆盖：用 `Queue<PersonalScorePredictionSnapshot>` 或绑定到具体一次跟打会话（绑定到 `Score.StartTime`）。文本刷新只更新"显示用 snapshot"，校准用"本轮起始 snapshot"。

---

#### BUG-H：`HasLearnedUnit` 只看 Count>0，单次样本就被信任

**定位**：`PersonalScorePredictor.cs:185-193`、`PersonalScorePredictor.cs:162-172`。

**现象**：用户偶然打了一次某词（卡顿/走神/IME 抖动），下次预测含该词的文章 DP 立即偏好用这一次的异常代价。

**影响**：单次离群样本主导整段文章的预测。`IsMature`（Count≥3 或 ObservedChars≥8）只影响置信度显示，**不**影响代价。

**修法**：
- DP 那边把 `HasLearnedUnit` 改成 `Count >= 2`（或更严格 ≥3）才允许多字段切分。
- `GetCost` 用贝叶斯收缩平滑：`avg = (stats.AverageMs*count + baselineMsPerChar*charCount*K) / (count + K)`，K=2~3 即可。一次样本被基线拽回，多次样本逐渐脱离基线。

---

### P1 - 体感问题或方法学问题

#### BUG-B：校准等权按"轮"，不按字数

**定位**：`PersonalPredictionCalibration.cs:127-135`。

**现象**：`+ 1` 让每轮等权。10 字短局和 1000 字长局对 TimeFactor 影响一样大。

**影响**：短局更易出极端 ratio（小样本噪声大），被 ClampRatio 钳到 0.25/4.0，污染因子。

**修法**：`Weight += chars`、`Ratio += ratio * chars`。

---

#### BUG-C：ratio 平均不等于真实偏移

**定位**：`PersonalPredictionCalibration.cs:127-130`。

**现象**：当前是 `Σ(actual/predicted) / N`，更稳的是 `Σ(actual) / Σ(predicted)`（字符数加权）。两者只在所有 ratio 相同时相等。

**举例**：actualSeconds=10/predictedSeconds=5（ratio=2）和 actualSeconds=100/predictedSeconds=100（ratio=1）。当前算法均值 1.5；总量加权 (10+100)/(5+100)=1.048。后者更代表稳态。

**修法**：和 BUG-B 一起改即可（按字数加权后两者等价）。

---

#### BUG-J：置信度阈值定义了却没用

**定位**：`PersonalScorePredictionFormatter.cs:9, 67-72` 定义了 `ScoreAttachmentConfidenceThreshold = 0.80` 和 `CanAttachToScore`，但 `PersonalScorePredictionService.AppendPredictionSnapshot` 没调用。

**现象**：0% 置信也照样把"预测速度/个难"贴到难度行。冷启动用户被极不准的数字劝退。

**修法**：`AppendPredictionSnapshot` / `AppendPrediction` 加 `if (Confidence < 显示阈值) return baseDifficultyText;`。两档阈值：
- 显示阈值（如 0.3）：低于这个完全不显示
- 强信任阈值（如 0.8，即现有常量）：高于这个才贴到成绩行

或者按置信度灰度淡化展示，让用户知道"还在学习中"。

---

### P2 - 设计偏差或可改进

#### BUG-D：PersonalDifficulty 用了已校准的 PredictedSpeed

**定位**：`PersonalScorePredictor.cs:126-127`。

```csharp
prediction.PredictedSpeed = n / (prediction.PredictedSeconds / 60.0);  // ← Seconds 已被 TimeFactor 校准
prediction.PersonalDifficultyScore = baseDifficultyScore * baselineSpeed / prediction.PredictedSpeed;
```

`baselineSpeed` 是历史实测平均（未被 TimeFactor 校准），`predictedSpeed` 是被 TimeFactor 校准后的。

**现象**：TimeFactor 偏大（用户实际比 DP 模型预测的慢）→ predictedSpeed 被压低 → PersonalDifficultyScore 被放大。但这部分"慢"其实是**模型系统误差**而非文本对用户难。TimeFactor 把模型误差伪装成了个人难度高。

**修法**：用未经 TimeFactor 校准的 raw DP 速度算 PersonalDifficultyScore；或者引入"baselineSpeed × 1/TimeFactor" 作为参照。需要明确：PersonalDifficulty 的物理意义是"这段对你来说比你的平均水平难多少"，分子分母要在同一坐标系。

---

#### BUG-I：陌生文本退化为基础难度

**定位**：`PersonalScorePredictor.cs:174-183`，`GetCost` fallback 分支。

**现象**：未学过的词代价 `charCount * 60000 / baselineSpeed` → 全陌生文章 predictedSpeed = baselineSpeed → PersonalDifficultyScore = baseDifficultyScore × 1 = baseDifficultyScore。用户感受："换一篇新文章个难和难度一样，个性化没生效"。

**修法**：fallback 代价乘 1.2~1.5 系数让陌生段慢于基线；或者按"该字符是否在 profile 出现过"分级——见过的单字基线、完全陌生的单字 ×1.5。

---

#### BUG-E：ClampRatio 边界是天花板/地板

**定位**：`PersonalPredictionCalibration.cs:169-174`。

**现象**：`Math.Max(0.25, Math.Min(4.0, ratio))`。如果用户实际长期慢于预测 5 倍以上，ratio 一直被钳到 4.0，TimeFactor 收敛到 4.0 而不是真实比值。下次预测仍偏小，用户继续被钳——**因子永远到不了真实位置**。冷启动期常见。

**修法**：
- 对被钳过的样本降低衰减权重（视为"该样本可信度低"）。
- 或改用 winsorize（钳但不计入分母），让 weight 反映"实际有效样本数"。
- 或者让 ClampRatio 的边界随累积 ObservedCharacters 收紧（前期宽松、稳定后收紧）。

---

#### BUG-F：冷启动期校准用废数据训练

**定位**：`PersonalScorePredictionService.cs:50-70`、`PersonalPredictionCalibration.cs:114-138`。

**现象**：profile 刚有数据但词条覆盖率极低时，DP 全走 fallback，PredictedSeconds 完全等于 `charCount * 60000 / 120 / 1000`（初始 BaselineSpeed=120）。此时 ratio 几乎只反映"用户速度和 120 的偏差"，不是"DP 模型误差"。校准在学一个没意义的常数。

**修法**：在 `Calibration.Add` 中加门控：当 `snapshot.Confidence < 0.5` 或 `profile.EffectiveStatCharacters < 阈值` 时跳过校准更新（但允许使用现有 factor 显示）。

---

#### BUG-G：Train + Calibrate 顺序耦合，无原子性

**定位**：`MainWindow.xaml.cs:4749-4756` + `PersonalScorePredictionService.Train/Calibrate`。

**现象**：
```csharp
service.Calibrate(snapshot, roundStats);   // Load profile → UpdateCalibration → Save
service.Train(..., roundStats);            // Load profile → Update → Save
```
两次 Load/Save 之间没原子性。当前单线程 StopHelper 调，**目前不出问题**但脆弱。SQLite 改造后两步合并为单事务即可一次性解决。

**修法**：SQLite store 暴露 `ApplyRound(snapshot, session, stats)` 单事务接口。

---

#### 设计 K：单调累积的 `EffectiveStatCharacters` 让 Baseline 终身均值

**定位**：`PersonalTypingProfile.cs:220`，`WeightedAverage(oldValue, oldChars, newValue, newChars)`。

**现象**：`EffectiveStatCharacters` 单调累加。打到几万字以后 oldWeight 远大于 newWeight，新数据几乎不影响 Baseline*。用户提速也反映不出来。和词条层（指数衰减）不一致。

**修法**：把 Baseline* 也用双轨指数衰减；或者 `EffectiveStatCharacters` 封顶（如 50000）。

---

#### 设计 L：跨提交段时间戳膨胀

**定位**：`PersonalTypingSessionBuilder.cs:128-131`。

**现象**：difficultySegmenter 给的额外段如果跨越多个提交边界，`charStartTimes[start]` 是该字所在那个提交的 `previousCommitTime`，整段 effective ms 把不属于该段的时间也计入。轻微膨胀样本耗时。

**修法**：要求段的 `[start, end)` 必须正好等于某些连续提交的并集（边界对齐）才采样；否则跳过。

---

### P3 - 容量 / 工程

#### 设计 M：profile JSON 无 SchemaVersion，Units 无淘汰

由 SQLite 改造一并解决。SQLite 改造完后 schema 自带版本（meta 表），Units 按 last_seen 淘汰。

---

## 三、建议修复顺序（第二波 PR 拆分）

**PR-A（一次性合并，行为可见改进）**：
- BUG-A：snapshot 错配（核心正确性）
- BUG-J：置信度显示门控（用户体验）
- BUG-H：单次就被信任（用户感受到的"预测乱跳"）

**PR-B（校准方法学，单独验证）**：
- BUG-B + C：按字数加权
- BUG-F：冷启动门控
- BUG-E：ClampRatio 改进

**PR-C（个人难度语义）**：
- BUG-D：PersonalDifficulty 用 raw speed
- BUG-I：陌生文本不退化

**PR-D（长期维护）**：
- 设计 K：Baseline 加衰减
- 设计 L：段时间戳对齐
- 设计 G：单事务（SQLite 改造完后自然实现）

---

## 四、不在本文范围

- SQLite 存储改造 → 见 `docs/2026-05-22-prediction-sqlite-plan.md`
- UI 显示项的开关/排序 → 已有 `PersonalScorePredictionFormatter.NormalizeOrder`，本文不涉及
