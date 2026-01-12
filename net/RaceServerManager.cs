using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TypeSunny.Net
{
    /// <summary>
    /// 赛文信息
    /// </summary>
    public class RaceInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int DifficultyGroup { get; set; }
        public bool AllowResubmit { get; set; }
        public bool IsActive { get; set; }

        /// <summary>
        /// 格式化显示赛文信息
        /// </summary>
        public string GetDisplayName()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return "未命名赛文";

            string diffStr = $"难度{DifficultyGroup}";
            string submitStr = AllowResubmit ? "可重复" : "每日一次";
            return $"{Name} ({diffStr}·{submitStr})";
        }
    }

    /// <summary>
    /// 赛文服务器配置
    /// </summary>
    public class RaceServer
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string DisplayName { get; set; }
        public int UserId { get; set; }
        public List<RaceInfo> Races { get; set; }
        public int CurrentRaceId { get; set; }  // 当前选中的赛文ID
        public int CurrentArticleId { get; set; }  // 当前文章ID
        public DateTime LastLoadTime { get; set; }  // 最后载文时间
        public string ClientKeyXml { get; set; }  // 客户端RSA密钥对（XML格式，持久化保存）

        // 记录每个赛文今天是否已提交（不可重复提交的赛文）：key = "日期_赛文ID", value = true
        [JsonIgnore]
        public Dictionary<string, bool> TodaySubmitted { get; set; }

        public RaceServer()
        {
            Id = Guid.NewGuid().ToString();
            Races = new List<RaceInfo>();
            UserId = -1;
            CurrentRaceId = -1;
            CurrentArticleId = -1;
            ClientKeyXml = "";  // 初始为空，首次使用时生成
            TodaySubmitted = new Dictionary<string, bool>();
        }

        /// <summary>
        /// 是否已登录
        /// </summary>
        public bool IsLoggedIn()
        {
            return UserId > 0 && !string.IsNullOrWhiteSpace(DisplayName);
        }

        /// <summary>
        /// 获取当前选中的赛文
        /// </summary>
        public RaceInfo GetCurrentRace()
        {
            return Races.FirstOrDefault(r => r.Id == CurrentRaceId);
        }

        /// <summary>
        /// 格式化显示服务器名称
        /// </summary>
        public string GetDisplayName()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return "未命名服务器";

            if (string.IsNullOrWhiteSpace(Url))
                return Name;

            Uri uri;
            string host = Url;
            if (Uri.TryCreate(Url, UriKind.Absolute, out uri))
            {
                host = uri.Host;
                if (uri.Port != 80 && uri.Port != 443)
                    host += ":" + uri.Port;
            }
            return $"{Name} [{host}]";
        }

        /// <summary>
        /// 检查今天某个赛文是否已提交（仅用于不可重复提交的赛文）
        /// </summary>
        public bool IsTodaySubmitted(int raceId)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string key = $"{today}_{raceId}";
            return TodaySubmitted.ContainsKey(key) && TodaySubmitted[key];
        }

        /// <summary>
        /// 标记今天某个赛文已提交
        /// </summary>
        public void MarkTodaySubmitted(int raceId)
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string key = $"{today}_{raceId}";
            TodaySubmitted[key] = true;
        }

        /// <summary>
        /// 检查赛文是否可以发文（考虑是否允许重复提交和今天是否已提交）
        /// </summary>
        public bool CanLoadArticle(int raceId)
        {
            var race = Races.FirstOrDefault(r => r.Id == raceId);
            if (race == null)
                return false;

            // 如果允许重复提交，总是可以发文
            if (race.AllowResubmit)
                return true;

            // 不允许重复提交，检查今天是否已提交
            return !IsTodaySubmitted(raceId);
        }
    }

    /// <summary>
    /// 赛文服务器管理器
    /// </summary>
    public class RaceServerManager
    {
        private List<RaceServer> servers;
        private string currentServerId;

        public RaceServerManager()
        {
            servers = new List<RaceServer>();
            currentServerId = null;
            LoadFromConfig();
        }

        /// <summary>
        /// 从配置文件加载
        /// </summary>
        public void LoadFromConfig()
        {
            try
            {
                string json = Config.GetString("赛文服务器配置");
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                    // 加载服务器列表
                    if (data != null && data.ContainsKey("servers"))
                    {
                        var serversJson = data["servers"].ToString();
                        servers = JsonConvert.DeserializeObject<List<RaceServer>>(serversJson) ?? new List<RaceServer>();
                    }

                    // 加载当前选中的服务器
                    if (data != null && data.ContainsKey("current_server_id"))
                    {
                        currentServerId = data["current_server_id"]?.ToString();
                    }
                }

                // 如果没有配置，创建默认服务器并保存
                if (servers == null || servers.Count == 0)
                {
                    AddDefaultServer(true);  // 立即保存
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载赛文服务器配置失败: {ex.Message}");
                // 创建默认服务器并保存
                servers = new List<RaceServer>();
                AddDefaultServer(true);  // 立即保存
            }
        }

        /// <summary>
        /// 保存到配置文件
        /// </summary>
        public void SaveToConfig()
        {
            try
            {
                if (servers == null)
                {
                    servers = new List<RaceServer>();
                }

                var data = new Dictionary<string, object>
                {
                    ["servers"] = servers,
                    ["current_server_id"] = currentServerId ?? ""
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                Config.Set("赛文服务器配置", json);
                Config.WriteConfig(0);  // 立即写入
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存赛文服务器配置失败: {ex.Message}");
                // 不抛出异常，避免程序崩溃
            }
        }

        /// <summary>
        /// 添加默认服务器
        /// </summary>
        /// <param name="saveImmediately">是否立即保存到配置文件</param>
        private void AddDefaultServer(bool saveImmediately = true)
        {
            try
            {
                string defaultUrl = Config.GetString("赛文服务器地址");

                if (string.IsNullOrWhiteSpace(defaultUrl))
                {
                    defaultUrl = "https://typing.fcxxz.com/";  // 使用默认值
                }

                var server = new RaceServer
                {
                    Name = "默认服务器",
                    Url = defaultUrl,
                    Username = Config.GetString("赛文用户名") ?? "",
                    Password = Config.GetString("赛文密码") ?? "",
                    DisplayName = Config.GetString("赛文显示名称") ?? "",
                    UserId = Config.GetInt("赛文用户ID")
                };

                // 尝试解析当前文章ID
                int.TryParse(Config.GetString("赛文当前文章ID"), out int articleId);
                server.CurrentArticleId = articleId;

                servers.Add(server);
                currentServerId = server.Id;

                // 只在需要时才立即保存
                if (saveImmediately)
                {
                    SaveToConfig();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"添加默认服务器失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取所有服务器
        /// </summary>
        public List<RaceServer> GetAllServers()
        {
            if (servers == null)
            {
                servers = new List<RaceServer>();
            }
            return servers;
        }

        /// <summary>
        /// 获取当前选中的服务器
        /// </summary>
        public RaceServer GetCurrentServer()
        {
            if (servers == null || servers.Count == 0)
                return null;

            if (string.IsNullOrWhiteSpace(currentServerId))
                return servers.FirstOrDefault();

            return servers.FirstOrDefault(s => s.Id == currentServerId);
        }

        /// <summary>
        /// 设置当前服务器
        /// </summary>
        public void SetCurrentServer(string serverId)
        {
            currentServerId = serverId;
            SaveToConfig();
        }

        /// <summary>
        /// 添加服务器
        /// </summary>
        public RaceServer AddServer(string name, string url)
        {
            if (servers == null)
            {
                servers = new List<RaceServer>();
            }

            var server = new RaceServer
            {
                Name = name,
                Url = url
            };

            servers.Add(server);

            // 如果是第一个服务器，自动设为当前服务器
            if (servers.Count == 1)
            {
                currentServerId = server.Id;
            }

            SaveToConfig();
            return server;
        }

        /// <summary>
        /// 删除服务器
        /// </summary>
        public bool RemoveServer(string serverId)
        {
            if (servers == null || servers.Count == 0)
                return false;

            var server = servers.FirstOrDefault(s => s.Id == serverId);
            if (server == null)
                return false;

            servers.Remove(server);

            // 如果删除的是当前服务器，切换到第一个服务器
            if (currentServerId == serverId)
            {
                currentServerId = servers.FirstOrDefault()?.Id;
            }

            SaveToConfig();
            return true;
        }

        /// <summary>
        /// 更新服务器配置
        /// </summary>
        public bool UpdateServer(RaceServer server)
        {
            if (servers == null || servers.Count == 0)
                return false;

            var existing = servers.FirstOrDefault(s => s.Id == server.Id);
            if (existing == null)
                return false;

            int index = servers.IndexOf(existing);
            servers[index] = server;
            SaveToConfig();
            return true;
        }

        /// <summary>
        /// 刷新服务器的赛文列表
        /// </summary>
        public async System.Threading.Tasks.Task<bool> RefreshServerRaces(string serverId)
        {
            if (servers == null || servers.Count == 0)
                return false;

            var server = servers.FirstOrDefault(s => s.Id == serverId);
            if (server == null)
                return false;

            try
            {
                var api = new RaceAPI(server.Url, server.ClientKeyXml);
                await api.InitializeAsync();

                var result = await api.GetRaceListAsync();
                if (!result.Success)
                    return false;

                // 调试输出：查看实际返回的数据
                System.Diagnostics.Debug.WriteLine("=== 赛文列表 API 返回数据 ===");
                System.Diagnostics.Debug.WriteLine(result.Data.ToString());
                System.Diagnostics.Debug.WriteLine("===========================");

                // 解析赛文列表
                var races = new List<RaceInfo>();

                // 尝试多种可能的数据格式
                Newtonsoft.Json.Linq.JToken racesToken = null;

                // 格式1: { "races": [...] }
                if (result.Data["races"] != null)
                {
                    racesToken = result.Data["races"];
                }
                // 格式2: { "data": { "races": [...] } }
                else if (result.Data["data"] != null)
                {
                    var dataObj = result.Data["data"];
                    if (dataObj.Type == Newtonsoft.Json.Linq.JTokenType.Object)
                    {
                        racesToken = dataObj["races"];
                    }
                }

                // 解析赛文数组
                if (racesToken != null && racesToken.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                {
                    foreach (var raceData in racesToken)
                    {
                        races.Add(new RaceInfo
                        {
                            Id = raceData["id"]?.ToObject<int>() ?? 0,
                            Name = raceData["name"]?.ToString() ?? "",
                            DifficultyGroup = raceData["difficulty_group"]?.ToObject<int>() ?? 1,
                            AllowResubmit = raceData["allow_resubmit"]?.ToObject<bool>() ?? false,
                            IsActive = true
                        });
                    }

                    System.Diagnostics.Debug.WriteLine($"成功解析 {races.Count} 个赛文");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("警告：未找到赛文数组，支持的格式：");
                    System.Diagnostics.Debug.WriteLine("  1. { \"races\": [...] }");
                    System.Diagnostics.Debug.WriteLine("  2. { \"data\": { \"races\": [...] } }");
                    System.Diagnostics.Debug.WriteLine("实际返回的数据结构：");
                    System.Diagnostics.Debug.WriteLine(result.Data.ToString());
                }

                server.Races = races;

                // 如果还没有选中赛文，自动选中第一个
                if (server.CurrentRaceId <= 0 && races.Count > 0)
                {
                    server.CurrentRaceId = races[0].Id;
                }

                SaveToConfig();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新服务器赛文列表失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 刷新所有服务器的赛文列表
        /// </summary>
        public async System.Threading.Tasks.Task RefreshAllServers()
        {
            if (servers == null || servers.Count == 0)
                return;

            foreach (var server in servers)
            {
                await RefreshServerRaces(server.Id);
            }
        }

        /// <summary>
        /// 更新服务器的登录信息
        /// </summary>
        public void UpdateServerLogin(string serverId, int userId, string username, string displayName)
        {
            if (servers == null || servers.Count == 0)
                return;

            var server = servers.FirstOrDefault(s => s.Id == serverId);
            if (server == null)
                return;

            server.UserId = userId;
            server.Username = username;
            server.DisplayName = displayName;
            SaveToConfig();
        }

        /// <summary>
        /// 设置服务器的当前赛文
        /// </summary>
        public void SetCurrentRace(string serverId, int raceId)
        {
            if (servers == null || servers.Count == 0)
                return;

            var server = servers.FirstOrDefault(s => s.Id == serverId);
            if (server == null)
                return;

            server.CurrentRaceId = raceId;
            SaveToConfig();
        }

        /// <summary>
        /// 设置服务器的当前文章
        /// </summary>
        public void SetCurrentArticle(string serverId, int articleId)
        {
            if (servers == null || servers.Count == 0)
                return;

            var server = servers.FirstOrDefault(s => s.Id == serverId);
            if (server == null)
                return;

            server.CurrentArticleId = articleId;
            server.LastLoadTime = DateTime.Now;
            SaveToConfig();
        }
    }
}
