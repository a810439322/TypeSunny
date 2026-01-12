using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace TypeSunny
{
    /// <summary>
    /// 赛文配置项
    /// </summary>
    public class RaceConfigItem
    {
        /// <summary>
        /// 赛文名称（显示在菜单上）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 赛文类型（用于标识使用哪个Helper类）
        /// jbs: 锦标赛
        /// jisucup: 极速杯
        /// race: 新赛文API
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 支持的功能列表
        /// </summary>
        public List<string> Features { get; set; }

        /// <summary>
        /// API地址（可选，用于自定义赛文源）
        /// </summary>
        public string ApiUrl { get; set; }

        public RaceConfigItem()
        {
            Features = new List<string>();
        }
    }

    /// <summary>
    /// 赛文配置管理器
    /// </summary>
    public static class RaceConfig
    {
        /// <summary>
        /// 获取所有赛文配置
        /// </summary>
        public static List<RaceConfigItem> GetRaceConfigs()
        {
            var configs = new List<RaceConfigItem>();

            // 首先添加内置的赛文源
            configs.AddRange(GetBuiltInConfigs());

            // 然后从配置文件读取自定义赛文源
            configs.AddRange(GetCustomConfigs());

            return configs;
        }

        /// <summary>
        /// 获取内置赛文配置（锦标赛、极速杯）
        /// </summary>
        private static List<RaceConfigItem> GetBuiltInConfigs()
        {
            var configs = new List<RaceConfigItem>();

            // 锦标赛配置
            configs.Add(new RaceConfigItem
            {
                Name = "锦标赛",
                Type = "jbs",
                Enabled = true,
                Features = new List<string> { "载文", "登录", "排行榜" }
            });

            // 极速杯配置
            configs.Add(new RaceConfigItem
            {
                Name = "极速杯",
                Type = "jisucup",
                Enabled = true,
                Features = new List<string> { "载文", "登录", "排行榜" }
            });

            // 赛文API配置
            string serverUrl = Config.GetString("赛文服务器地址");

            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                serverUrl = "https://typing.fcxxz.com/";  // 使用默认值
            }

            configs.Add(new RaceConfigItem
            {
                Name = "赛文API",
                Type = "race",
                Enabled = true,
                Features = new List<string> { "载文", "登录", "注册", "历史", "排行榜" },
                ApiUrl = serverUrl
            });

            return configs;
        }

        /// <summary>
        /// 从配置文件获取自定义赛文源
        /// </summary>
        private static List<RaceConfigItem> GetCustomConfigs()
        {
            var configs = new List<RaceConfigItem>();

            try
            {
                // 从Config读取自定义赛文JSON配置
                string customConfigJson = Config.GetString("自定义赛文配置");

                if (!string.IsNullOrWhiteSpace(customConfigJson))
                {
                    var customConfigs = JsonConvert.DeserializeObject<List<RaceConfigItem>>(customConfigJson);
                    if (customConfigs != null)
                    {
                        configs.AddRange(customConfigs);
                    }
                }
            }
            catch (Exception ex)
            {
                // 解析失败时忽略
                System.Diagnostics.Debug.WriteLine($"解析自定义赛文配置失败: {ex.Message}");
            }

            return configs;
        }

        /// <summary>
        /// 保存自定义赛文配置
        /// </summary>
        public static void SaveCustomConfigs(List<RaceConfigItem> configs)
        {
            try
            {
                string json = JsonConvert.SerializeObject(configs, Formatting.Indented);
                Config.Set("自定义赛文配置", json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存自定义赛文配置失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 示例：创建一个新赛文API配置
        /// </summary>
        public static RaceConfigItem CreateNewRaceApiConfig()
        {
            return new RaceConfigItem
            {
                Name = "赛文API",
                Type = "race",
                Enabled = true,
                Features = new List<string> { "载文", "登录", "排行榜", "历史" },
                ApiUrl = Config.GetString("赛文服务器地址")
            };
        }
    }
}
