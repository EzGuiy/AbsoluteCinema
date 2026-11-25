using System.IO;
using System.Text.Json;

namespace Kawin_67091861_002.Helpers
{
    public class UserProfileData
    {
        public int UserId { get; set; }
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Role { get; set; } = "User";
        public string CreatedAt { get; set; } = "";
    }

    public static class UserProfileHelper
    {
        private static readonly object _lock = new();

        private static string GetFilePath(string? contentRootPath = null)
        {
            var baseDir = !string.IsNullOrEmpty(contentRootPath) ? contentRootPath : Directory.GetCurrentDirectory();
            var dir = Path.Combine(baseDir, "Data");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "users_profile.json");
        }

        public static List<UserProfileData> GetAll(string? contentRootPath = null)
        {
            lock (_lock)
            {
                var file = GetFilePath(contentRootPath);
                if (!File.Exists(file)) return new List<UserProfileData>();

                try
                {
                    var json = File.ReadAllText(file);
                    return JsonSerializer.Deserialize<List<UserProfileData>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<UserProfileData>();
                }
                catch
                {
                    return new List<UserProfileData>();
                }
            }
        }

        public static void SaveOrUpdate(string contentRootPath, int userId, string email, string fullName, string phone, string role = "User")
        {
            lock (_lock)
            {
                var list = GetAll(contentRootPath);
                email = (email ?? "").Trim().ToLowerInvariant();

                var existing = list.FirstOrDefault(u => 
                    (userId > 0 && u.UserId == userId) || 
                    (!string.IsNullOrEmpty(email) && u.Email.Trim().ToLowerInvariant() == email));

                if (existing != null)
                {
                    if (userId > 0) existing.UserId = userId;
                    if (!string.IsNullOrEmpty(fullName)) existing.FullName = fullName;
                    if (!string.IsNullOrEmpty(phone)) existing.Phone = phone;
                    if (!string.IsNullOrEmpty(role)) existing.Role = role;
                }
                else
                {
                    list.Add(new UserProfileData
                    {
                        UserId = userId,
                        Email = email,
                        FullName = fullName ?? "",
                        Phone = phone ?? "",
                        Role = role,
                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    });
                }

                try
                {
                    var file = GetFilePath(contentRootPath);
                    var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(file, json);
                }
                catch { }
            }
        }

        public static void SaveOrUpdate(int userId, string email, string phone, string fullName = "", string role = "User")
        {
            SaveOrUpdate(Directory.GetCurrentDirectory(), userId, email, fullName, phone, role);
        }

        public static string GetPhone(string contentRootPath, int userId, string email)
        {
            lock (_lock)
            {
                var list = GetAll(contentRootPath);
                email = (email ?? "").Trim().ToLowerInvariant();

                var item = list.FirstOrDefault(u => 
                    (userId > 0 && u.UserId == userId) || 
                    (!string.IsNullOrEmpty(email) && u.Email.Trim().ToLowerInvariant() == email));

                return item?.Phone ?? "";
            }
        }

        public static string GetPhone(string contentRootPath, string? email, int userId = 0)
        {
            return GetPhone(contentRootPath, userId, email ?? "");
        }

        public static string GetPhone(string? email, int userId = 0)
        {
            return GetPhone(Directory.GetCurrentDirectory(), userId, email ?? "");
        }
    }
}
