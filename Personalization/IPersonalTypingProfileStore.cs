using System;
using System.Collections.Generic;

namespace TypeSunny.Personalization
{
    /// <summary>
    /// 个人化打字画像的存储接口。
    ///
    /// 在 SQLite 化之前，画像是单 JSON 文件全量加载/全量重写；之后改为按需查询、增量写入。
    /// 该接口同时支持两种使用方式：
    ///
    /// - 全量初始化（测试 / 数据迁移）：使用 <see cref="Save"/> 把一个完整 profile 一次性写入；
    ///   使用 <see cref="Load"/> 拿到 baseline + calibration（但 Units 为空字典，运行时不该再依赖
    ///   遍历 Units），这是测试 setup 用的兼容入口。
    /// - 运行时按需访问：使用 <see cref="LoadWithUnits"/> 只拉取本次预测/训练真正涉及的 unit；
    ///   使用 <see cref="ApplyTraining"/> 和 <see cref="ApplyCalibration"/> 在事务中只写改动部分。
    ///
    /// 实现需保证线程安全（System.Data.SQLite 的 Connection 非线程安全，调用方加锁即可）。
    /// </summary>
    internal interface IPersonalTypingProfileStore : IDisposable
    {
        /// <summary>
        /// 兼容性入口：返回 Baseline + Calibration + 全部 Units 的 profile（全量读取）。
        /// 该入口主要给测试 setup、离线工具和数据迁移用，规模较大时会一次性加载全部 unit；
        /// 运行时请优先使用 <see cref="LoadWithUnits"/> 按需拉取。
        /// </summary>
        PersonalTypingProfile Load();

        /// <summary>
        /// 按需加载：返回 Baseline + Calibration 完整、Units 仅包含 <paramref name="texts"/>
        /// 中查到的词条的 profile。未传入的 unit 不会出现在返回值的 Units 中。
        /// </summary>
        PersonalTypingProfile LoadWithUnits(IEnumerable<string> texts);

        /// <summary>
        /// 兼容性入口：把 profile 整把保存（Baseline + Calibration + 全部 Units）。
        /// 运行时优先使用 <see cref="ApplyTraining"/> / <see cref="ApplyCalibration"/> 的增量写入。
        /// 该方法主要给测试 setup 与历史迁移使用。
        /// </summary>
        void Save(PersonalTypingProfile profile);

        /// <summary>
        /// 单事务写入：更新 Baseline + UPSERT <paramref name="updatedProfileWithChangedUnitsOnly"/>
        /// 中 Units 列出的全部词条。不会触碰 profile 没有列出的 unit。
        /// </summary>
        void ApplyTraining(PersonalTypingProfile updatedProfileWithChangedUnitsOnly);

        /// <summary>
        /// 单事务写入校准行（不动 Baseline / Units）。
        /// </summary>
        void ApplyCalibration(PersonalPredictionCalibration calibration);
    }
}
