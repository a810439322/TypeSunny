using System;
using System.IO;

namespace TypeSunny.Tests
{
    internal static class ReleasePublishedTimeFlowTests
    {
        private static int Main(string[] args)
        {
            try
            {
                string root = args.Length > 0
                    ? args[0]
                    : Directory.GetCurrentDirectory();

                string versionManager = Read(root, "Version", "VersionManager.cs");
                string packageIdentity = Read(root, "Version", "ReleasePackageIdentity.cs");
                string compiledIdentity = Read(root, "Version", "CompiledReleaseIdentity.cs");
                string config = Read(root, "Config", "Config.cs");
                string dialog = Read(root, "UI", "UpdateDialog.xaml.cs");
                string downloader = Read(root, "Utils", "UpdatePackageDownloader.cs");
                string updater = Read(root, "Updater", "Program.cs");
                string updaterProject = Read(root, "Updater", "Updater.csproj");
                string project = Read(root, "TypeSunny.csproj");

                RequireFile(root, "Version", "ReleaseIdentity.cs");
                RequireFile(root, "Version", "CompiledReleaseIdentity.cs");
                RequireFile(root, "Version", "ReleasePackageIdentity.cs");
                RequireFile(root, "Updater", "UpdaterConfigWriter.cs");

                Require(project, "Version\\ReleaseIdentity.cs", "project should include ReleaseIdentity.cs");
                Require(project, "Version\\CompiledReleaseIdentity.cs", "project should include CompiledReleaseIdentity.cs");
                Require(project, "Version\\ReleasePackageIdentity.cs", "project should include ReleasePackageIdentity.cs");
                Require(config, "最新发布UTC时间", "config should include latest release UTC time");
                Require(config, "已安装版本", "config should include installed version");
                Require(config, "已安装发布UTC时间", "config should include installed release UTC time");

                Require(versionManager, "LatestReleasePublishedUtc", "version manager should expose latest release UTC time");
                Require(versionManager, "LatestReleasePublishedBeijingTime", "version manager should expose Beijing display time");
                Require(versionManager, "packageManifestUrl", "version manager should detect package manifest asset");
                Require(versionManager, "ReadPackageManifestPublishedUtcAsync", "version manager should read package manifest time");
                Require(versionManager, "ResolvePublishedUtc", "version manager should prefer package time over release time");
                Require(versionManager, "EnsureInstalledReleaseIdentity", "version manager should self-heal installed release identity");
                Require(versionManager, "CompiledReleaseIdentity.PackagePublishedUtcTicks", "version manager should read compiled package identity");
                Require(versionManager, "InstalledReleasePublishedUtc >= publishedUtc", "version manager should not replace a newer updater-written installed identity");
                Require(compiledIdentity, "PackagePublishedUtcTicks", "compiled identity should expose the packaged publish time ticks");
                Require(compiledIdentity, "ReleaseIdentity.FromUtcTicks", "compiled identity should expose UTC publish time");
                Require(packageIdentity, "published_at", "package identity should parse published_at");
                Require(packageIdentity, "created_at", "package identity should parse created_at");
                Require(packageIdentity, "updated_at", "package identity should parse updated_at");
                Require(packageIdentity, "package_published_at", "package identity should parse package_published_at");
                Require(packageIdentity, "ParseReleasePublishedUtc", "package identity should parse release time");
                Require(packageIdentity, "ParseAssetPublishedUtc", "package identity should parse asset time");
                Require(packageIdentity, "ParseManifestPublishedUtc", "package identity should parse manifest time");
                Require(versionManager, "ReleaseIdentity.HasUpdate", "version manager should use timed update check");
                Require(versionManager, "ReleaseIdentity.IsIgnored", "version manager should use timed ignore check");
                Require(versionManager, "IgnoredVersion = ReleaseIdentity.Build", "version manager should store timed ignore identity");

                Require(dialog, "发布于", "update dialog should show published time");
                Require(dialog, "LatestReleasePublishedBeijingTime", "update dialog should use Beijing display property");

                Require(downloader, "VersionManager.LatestVersion", "downloader should pass latest version to updater");
                Require(downloader, "LatestReleasePublishedUtc", "downloader should pass latest release ticks to updater");
                Require(downloader, "UpdatePackageStager.StageUpdater", "downloader should launch updater staged from the downloaded package");
                Require(updater, "string installedVersion = args.Length >= 5", "updater should read optional installed version argument");
                Require(updater, "string installedReleaseUtcTicks = args.Length >= 6", "updater should read optional installed release time argument");
                Require(updater, "UpdaterConfigWriter.SaveInstalledRelease", "updater should save installed release identity after extraction");
                Require(updater, "updaterExePath", "updater should know which updater executable is running");
                Require(updaterProject, "UpdaterConfigWriter.cs", "updater project should include config writer");

                Console.WriteLine("All release published time flow tests passed.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static string Read(string root, params string[] paths)
        {
            return File.ReadAllText(Path.Combine(Combine(root, paths)));
        }

        private static void RequireFile(string root, params string[] paths)
        {
            string path = Combine(root, paths);
            if (!File.Exists(path))
                throw new Exception("expected file to exist: " + path);
        }

        private static void Require(string content, string needle, string message)
        {
            if (!content.Contains(needle))
                throw new Exception(message + " missing [" + needle + "]");
        }

        private static string Combine(string root, string[] paths)
        {
            string path = root;
            foreach (string item in paths)
                path = Path.Combine(path, item);
            return path;
        }
    }
}
