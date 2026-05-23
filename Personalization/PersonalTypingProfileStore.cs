using System;
using System.Collections.Generic;
using System.IO;

namespace TypeSunny.Personalization
{
    /// <summary>
    /// 个人化画像存储的对外门面（thin wrapper）。
    ///
    /// 历史版本是把整张 profile 序列化成 JSON 写到 <c>预测日志/PersonalTypingProfile.json</c>。
    /// SQLite 化之后，文件路径默认改成 <c>预测日志/profile.db</c>，旧 JSON 在首次打开时由
    /// <see cref="SqlitePersonalTypingProfileStore"/> 自动迁移并删除。
    ///
    /// 为了让现有测试代码与调用点 (<c>new PersonalTypingProfileStore(tempPath)</c>) 在不改签名的
    /// 前提下复用临时文件路径，本类的构造函数会把带 <c>.json</c> 后缀的路径透明地映射成 <c>.db</c>。
    /// </summary>
    internal sealed class PersonalTypingProfileStore : IPersonalTypingProfileStore
    {
        private readonly SqlitePersonalTypingProfileStore inner;

        public PersonalTypingProfileStore()
            : this(GetDefaultDbPath())
        {
        }

        public PersonalTypingProfileStore(string path)
        {
            string dbPath = NormalizePathToDb(path);
            this.inner = new SqlitePersonalTypingProfileStore(dbPath);
        }

        public PersonalTypingProfile Load()
        {
            return inner.Load();
        }

        public PersonalTypingProfile LoadWithUnits(IEnumerable<string> texts)
        {
            return inner.LoadWithUnits(texts);
        }

        public void Save(PersonalTypingProfile profile)
        {
            inner.Save(profile);
        }

        public void ApplyTraining(PersonalTypingProfile updatedProfileWithChangedUnitsOnly)
        {
            inner.ApplyTraining(updatedProfileWithChangedUnitsOnly);
        }

        public void ApplyCalibration(PersonalPredictionCalibration calibration)
        {
            inner.ApplyCalibration(calibration);
        }

        public void Dispose()
        {
            inner.Dispose();
        }

        private static string GetDefaultDbPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "预测日志", "profile.db");
        }

        /// <summary>
        /// 把传入路径规整为 .db 文件路径：
        /// - 以 ".json" 结尾的旧式路径会替换成 ".db"（保留对历史调用方与测试的兼容性）；
        /// - 其它后缀（含 ".db"、".sqlite" 等）原样使用。
        /// </summary>
        internal static string NormalizePathToDb(string path)
        {
            if (string.IsNullOrEmpty(path))
                return GetDefaultDbPath();

            if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return path.Substring(0, path.Length - ".json".Length) + ".db";

            return path;
        }
    }
}
