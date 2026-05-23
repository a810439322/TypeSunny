using System.Collections.Generic;

namespace TypeSunny.Personalization
{
    /// <summary>
    /// 旧 JSON 存储格式的 DTO，仅用于 SQLite 首次迁移时反序列化 <c>PersonalTypingProfile.json</c>。
    ///
    /// 字段顺序、命名必须与 <see cref="PersonalTypingProfile"/> 保持一致（Newtonsoft.Json 默认按属性
    /// 名匹配），以便老版本写出的 JSON 能完整恢复。这里独立成 DTO 是为了让正主类 (PersonalTypingProfile)
    /// 后续若有字段调整不影响历史 JSON 兼容。
    /// </summary>
    internal sealed class LegacyJsonProfile
    {
        public int EffectiveStatCharacters { get; set; }
        public double BaselineSpeed { get; set; }
        public double BaselineHitRate { get; set; }
        public double BaselineKpw { get; set; }
        public double BaselineAccuracy { get; set; }
        public double BaselineBacksPerChar { get; set; }
        public double BaselineCorrectionPerChar { get; set; }
        public double BaselineWasteCodesPerChar { get; set; }
        public double BaselineChoosePerChar { get; set; }
        public PersonalPredictionCalibration Calibration { get; set; }
        public Dictionary<string, PersonalTypingUnitStats> Units { get; set; }

        public LegacyJsonProfile()
        {
            BaselineSpeed = 120;
            BaselineHitRate = 5;
            BaselineKpw = 4;
            BaselineAccuracy = 98;
            Calibration = new PersonalPredictionCalibration();
            Units = new Dictionary<string, PersonalTypingUnitStats>();
        }
    }
}
