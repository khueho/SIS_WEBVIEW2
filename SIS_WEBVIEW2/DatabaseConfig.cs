using System;
using System.IO;
using System.Text.Json;

namespace SIS_WEBVIEW2
{
    public class DbConfig
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 5432;
        public string Database { get; set; } = "sis_patient_db";
        public string Username { get; set; } = "postgres";
        public string Password { get; set; } = "password";
        public string? ConnectionString { get; set; }

        public string BuildConnectionString()
        {
            if (!string.IsNullOrWhiteSpace(ConnectionString))
            {
                return ConnectionString.Trim();
            }

            return $"Host={Host};Port={Port};Database={Database};Username={Username};Password={Password};Include Error Detail=true;";
        }
    }

    public static class DatabaseConfigService
    {
        private static DbConfig? _cachedConfig;
        private static readonly string ConfigFileName = "db_config.json";

        /// <summary>
        /// Tải cấu hình kết nối database từ db_config.json
        /// </summary>
        public static DbConfig LoadConfig(bool forceReload = false)
        {
            if (!forceReload && _cachedConfig != null) return _cachedConfig;

            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
            if (!File.Exists(configPath))
            {
                string fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);
                if (File.Exists(fallbackPath)) configPath = fallbackPath;
            }

            if (File.Exists(configPath))
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    var cfg = JsonSerializer.Deserialize<DbConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (cfg != null)
                    {
                        _cachedConfig = cfg;
                        return _cachedConfig;
                    }
                }
                catch
                {
                    // Fallback mặc định nếu lỗi parse JSON
                }
            }

            _cachedConfig = new DbConfig();
            return _cachedConfig;
        }

        /// <summary>
        /// Lấy chuỗi kết nối PostgreSQL từ file cấu hình
        /// </summary>
        public static string GetConnectionString()
        {
            return LoadConfig().BuildConnectionString();
        }
    }
}
