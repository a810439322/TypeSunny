# System.Data.SQLite 二进制部署说明

本目录存放 System.Data.SQLite 的预编译二进制。不走 NuGet，原因见 `docs/2026-05-22-prediction-sqlite-plan.md` 第二节。

## 下载来源

https://system.data.sqlite.org/index.html/doc/trunk/www/downloads.wiki

选择 "Precompiled Binaries for .NET 4.6"（最新稳定版 1.0.119 或 1.0.118），下载 **x86** 和 **x64** 两份非 bundle 版本（"Setups for ..." 不要选，要 "Precompiled Binaries"）。

## 放置位置

```
lib/SQLite/
├── README.md                       (本文件)
├── LICENSE.txt                     (从下载包里取，Public Domain 声明)
├── System.Data.SQLite.dll          (托管程序集，从 x86 或 x64 包里都行，两者相同)
├── x86/
│   └── SQLite.Interop.dll          (从 32 位包里取)
└── x64/
    └── SQLite.Interop.dll          (从 64 位包里取)
```

`System.Data.SQLite.dll` 在运行时按进程位数从 `x86/` 或 `x64/` 子目录加载 native interop。Release 配置当前 `PlatformTarget=x86` 用 x86，Debug AnyCPU 在 64 位 Windows 上以 64 位进程运行用 x64。两份都要部署。

## csproj 已有配置

`TypeSunny.csproj` 中：

```xml
<Reference Include="System.Data.SQLite">
  <HintPath>lib\SQLite\System.Data.SQLite.dll</HintPath>
  <Private>True</Private>
</Reference>

<Content Include="lib\SQLite\x86\SQLite.Interop.dll">
  <Link>x86\SQLite.Interop.dll</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
<Content Include="lib\SQLite\x64\SQLite.Interop.dll">
  <Link>x64\SQLite.Interop.dll</Link>
  <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
</Content>
```

只要按上面目录结构放好文件，编译时会自动复制到 `bin/<Configuration>/x86/` 和 `bin/<Configuration>/x64/`。

## 版本要求

至少需要：
- 兼容 .NET Framework 4.6+（本项目用 4.8）
- 自带 `SQLite.Interop.dll`（即非 2.0+ 的 split 版本）
- 1.0.118 或 1.0.119 都可

> 注：1.0.x 系列的 `SQLite.Interop.dll` 内置 SQLite 引擎，没有额外 `e_sqlite3.dll` 依赖。2.0+ 系列拆分了引擎，部署更复杂，**不要用 2.0+**。

## 验证

部署完成后构建项目，输出目录应有：

```
bin/Release/
├── 晴跟打.exe
├── System.Data.SQLite.dll
├── x86/
│   └── SQLite.Interop.dll          (Release x86 实际加载这个)
└── x64/
    └── SQLite.Interop.dll
```

首次运行启用预测，应在 `预测日志/` 目录生成 `profile.db`（以及 WAL 模式的 `profile.db-wal` 和 `profile.db-shm` 伴随文件）。
