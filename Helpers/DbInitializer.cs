using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Kawin_67091861_002.Models;

namespace Kawin_67091861_002.Helpers
{
    public static class DbInitializer
    {
        private static readonly object _lock = new();
        private static bool _initialized = false;

        public static void Initialize(AbsoluteCinemaContext db, string contentRootPath)
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                try
                {
                    if (!db.Database.CanConnect()) return;

                    // 1. Roles
                    var roles = new[] { "Admin", "Manager", "Staff", "Technician", "Accountant", "User" };
                    var existingRoles = db.Roles.ToList();
                    bool rolesChanged = false;
                    foreach (var r in roles)
                    {
                        if (!existingRoles.Any(er => er.RoleName.Equals(r, StringComparison.OrdinalIgnoreCase)))
                        {
                            db.Roles.Add(new Role { RoleName = r });
                            rolesChanged = true;
                        }
                    }
                    if (rolesChanged)
                    {
                        db.SaveChanges();
                        existingRoles = db.Roles.ToList();
                    }

                    int adminRoleId = existingRoles.FirstOrDefault(r => r.RoleName == "Admin")?.RoleId ?? 1;
                    int managerRoleId = existingRoles.FirstOrDefault(r => r.RoleName == "Manager")?.RoleId ?? adminRoleId;
                    int staffRoleId = existingRoles.FirstOrDefault(r => r.RoleName == "Staff")?.RoleId ?? adminRoleId;
                    int techRoleId = existingRoles.FirstOrDefault(r => r.RoleName == "Technician")?.RoleId ?? adminRoleId;
                    int acctRoleId = existingRoles.FirstOrDefault(r => r.RoleName == "Accountant")?.RoleId ?? adminRoleId;
                    int userRoleId = existingRoles.FirstOrDefault(r => r.RoleName == "User")?.RoleId ?? adminRoleId;

                    // 2. Locked Users for Every Role
                    var lockedUsers = new[]
                    {
                        (Username: "admin", FullName: "ผู้ดูแลระบบสาขา (Admin)", Password: "password123", RoleId: adminRoleId, Phone: "080-000-0001", RoleName: "Admin"),
                        (Username: "admin@absolutecinema.com", FullName: "ผู้ดูแลระบบสาขา (Admin)", Password: "password123", RoleId: adminRoleId, Phone: "080-000-0001", RoleName: "Admin"),
                        (Username: "manager", FullName: "ณัฐชา วัฒนากุล (ผู้จัดการสาขา)", Password: "password123", RoleId: managerRoleId, Phone: "080-000-0002", RoleName: "Manager"),
                        (Username: "manager@absolutecinema.com", FullName: "ณัฐชา วัฒนากุล (ผู้จัดการสาขา)", Password: "password123", RoleId: managerRoleId, Phone: "080-000-0002", RoleName: "Manager"),
                        (Username: "staff", FullName: "สมศรี รักบริการ (พนักงานบริการ)", Password: "password123", RoleId: staffRoleId, Phone: "080-000-0003", RoleName: "Staff"),
                        (Username: "staff@absolutecinema.com", FullName: "สมศรี รักบริการ (พนักงานบริการ)", Password: "password123", RoleId: staffRoleId, Phone: "080-000-0003", RoleName: "Staff"),
                        (Username: "technician", FullName: "อนุชา ทนทาน (ช่างเทคนิค)", Password: "password123", RoleId: techRoleId, Phone: "080-000-0004", RoleName: "Technician"),
                        (Username: "tech@absolutecinema.com", FullName: "อนุชา ทนทาน (ช่างเทคนิค)", Password: "password123", RoleId: techRoleId, Phone: "080-000-0004", RoleName: "Technician"),
                        (Username: "accountant", FullName: "ณิชา พงษ์ศิริ (ฝ่ายบัญชี)", Password: "password123", RoleId: acctRoleId, Phone: "080-000-0005", RoleName: "Accountant"),
                        (Username: "acct@absolutecinema.com", FullName: "ณิชา พงษ์ศิริ (ฝ่ายบัญชี)", Password: "password123", RoleId: acctRoleId, Phone: "080-000-0005", RoleName: "Accountant"),
                        (Username: "member", FullName: "กิตติพงษ์ สุขใจ (สมาชิกทั่วไป)", Password: "password123", RoleId: userRoleId, Phone: "089-123-4567", RoleName: "User"),
                        (Username: "member@absolutecinema.com", FullName: "กิตติพงษ์ สุขใจ (สมาชิกทั่วไป)", Password: "password123", RoleId: userRoleId, Phone: "089-123-4567", RoleName: "User")
                    };

                    var existingUsers = db.Users.ToList();
                    bool usersChanged = false;

                    foreach (var u in lockedUsers)
                    {
                        var found = existingUsers.FirstOrDefault(eu => eu.Username.Equals(u.Username, StringComparison.OrdinalIgnoreCase));
                        if (found == null)
                        {
                            var newUser = new User
                            {
                                Username = u.Username,
                                FullName = u.FullName,
                                PasswordHash = u.Password,
                                RoleId = u.RoleId,
                                CreatedAt = DateTime.Now
                            };
                            db.Users.Add(newUser);
                            usersChanged = true;
                        }
                        else
                        {
                            // Ensure password and role are locked and accurate
                            found.PasswordHash = u.Password;
                            found.FullName = u.FullName;
                            found.RoleId = u.RoleId;
                            usersChanged = true;
                        }

                        // Store phone in UserProfileHelper
                        UserProfileHelper.SaveOrUpdate(contentRootPath, 0, u.Username, u.FullName, u.Phone, u.RoleName);
                    }

                    if (usersChanged)
                    {
                        db.SaveChanges();
                    }

                    // 3. Cinemas
                    if (!db.Cinemas.Any())
                    {
                        db.Cinemas.AddRange(
                            new Cinema { CinemaId = 1, CinemaName = "Ultra Screen (Normal)", TotalSeats = 100, Status = "Ready" },
                            new Cinema { CinemaId = 2, CinemaName = "Gold Class (Premium)", TotalSeats = 100, Status = "Ready" },
                            new Cinema { CinemaId = 3, CinemaName = "The Bed Cinema (VIP)", TotalSeats = 60, Status = "Ready" },
                            new Cinema { CinemaId = 4, CinemaName = "Cinema 4 (Digital 4K)", TotalSeats = 120, Status = "Ready" }
                        );
                        db.SaveChanges();
                    }

                    // 4. Movies (Read from Data/movies.json if available so custom movies are preserved)
                    if (!db.Movies.Any())
                    {
                        bool seededFromJson = false;
                        try
                        {
                            var moviesJsonPath = Path.Combine(contentRootPath, "Data", "movies.json");
                            if (File.Exists(moviesJsonPath))
                            {
                                var jsonText = File.ReadAllText(moviesJsonPath);
                                using var doc = System.Text.Json.JsonDocument.Parse(jsonText);
                                if (doc.RootElement.TryGetProperty("Movies", out var moviesEl))
                                {
                                    foreach (var m in moviesEl.EnumerateArray())
                                    {
                                        string title = m.TryGetProperty("Title", out var tProp) ? tProp.GetString() ?? "" : "";
                                        if (string.IsNullOrWhiteSpace(title)) continue;

                                        db.Movies.Add(new Movie
                                        {
                                            Title = title,
                                            DurationMins = m.TryGetProperty("DurationMins", out var dm) ? dm.GetInt32() : 120,
                                            Genre = m.TryGetProperty("Genre", out var gn) ? gn.GetString() : "ทั่วไป",
                                            Description = m.TryGetProperty("Description", out var ds) ? ds.GetString() : "",
                                            Rating = m.TryGetProperty("Rating", out var rt) ? rt.GetDouble() : 8.5,
                                            PriceMin = m.TryGetProperty("PriceMin", out var pmin) ? pmin.GetDecimal() : 1400,
                                            PriceMax = m.TryGetProperty("PriceMax", out var pmax) ? pmax.GetDecimal() : 3500,
                                            PosterUrl = m.TryGetProperty("PosterUrl", out var pu) ? pu.GetString() : null,
                                            Status = m.TryGetProperty("Status", out var st) ? st.GetString() ?? "Showing" : "Showing",
                                            ReleaseDate = DateTime.Today.AddDays(-5)
                                        });
                                        seededFromJson = true;
                                    }
                                    if (seededFromJson) db.SaveChanges();
                                }
                            }
                        }
                        catch { }

                        if (!seededFromJson)
                        {
                            db.Movies.AddRange(
                                new Movie
                                {
                                    Title = "ซุส",
                                    DurationMins = 135,
                                    Genre = "องค์ลง / ตลก",
                                    Description = "หนังเรื่องนี้เหมาะสำหรับขั้นเทพเท่านั้นที่ดูได้",
                                    Rating = 9.5,
                                    PriceMin = 1400,
                                    PriceMax = 3500,
                                    PosterUrl = "/images/zeus.jpg",
                                    Status = "Showing",
                                    ReleaseDate = DateTime.Today.AddDays(-10)
                                },
                                new Movie
                                {
                                    Title = "โหน่งเท่ง",
                                    DurationMins = 110,
                                    Genre = "ตลกหกสิบฉาก",
                                    Description = "ไหนน้าหม่ำ",
                                    Rating = 10.0,
                                    PriceMin = 1400,
                                    PriceMax = 3500,
                                    PosterUrl = "/images/tengmam.jpg",
                                    Status = "Showing",
                                    ReleaseDate = DateTime.Today.AddDays(-20)
                                }
                            );
                            db.SaveChanges();
                        }
                    }

                    // 5. Ensure Showtimes for today through next 5 days
                    var dbMovies = db.Movies.ToList();
                    var dbCinemas = db.Cinemas.ToList();
                    if (dbMovies.Any() && dbCinemas.Any())
                    {
                        var existingShowtimes = db.Showtimes.ToList();
                        for (int dayOffset = 0; dayOffset < 5; dayOffset++)
                        {
                            var targetDate = DateTime.Today.AddDays(dayOffset);
                            foreach (var m in dbMovies)
                            {
                                foreach (var c in dbCinemas.Take(3))
                                {
                                    // 3 slots per cinema per movie
                                    int[] hours = c.CinemaId switch
                                    {
                                        1 => new[] { 11, 14, 18 },
                                        2 => new[] { 12, 15, 19 },
                                        3 => new[] { 13, 17, 21 },
                                        _ => new[] { 12, 16, 20 }
                                    };

                                    int[] minutes = c.CinemaId switch
                                    {
                                        1 => new[] { 30, 15, 0 },
                                        _ => new[] { 0, 0, 0 }
                                    };

                                    for (int sIdx = 0; sIdx < hours.Length; sIdx++)
                                    {
                                        var st = targetDate.AddHours(hours[sIdx]).AddMinutes(minutes[sIdx]);
                                        var et = st.AddMinutes(m.DurationMins > 0 ? m.DurationMins : 120);

                                        bool exists = existingShowtimes.Any(s => s.MovieId == m.MovieId && s.CinemaId == c.CinemaId && s.StartTime == st);
                                        if (!exists)
                                        {
                                            db.Showtimes.Add(new Showtime
                                            {
                                                MovieId = m.MovieId,
                                                CinemaId = c.CinemaId,
                                                StartTime = st,
                                                EndTime = et
                                            });
                                        }
                                    }
                                }
                            }
                        }
                        db.SaveChanges();
                    }

                    // 6. Ensure Chats Table has team group messages
                    if (!db.Chats.Any())
                    {
                        var staffUser = db.Users.FirstOrDefault(u => u.Username == "staff") ?? db.Users.First();
                        var managerUser = db.Users.FirstOrDefault(u => u.Username == "manager") ?? db.Users.First();
                        var techUser = db.Users.FirstOrDefault(u => u.Username == "technician") ?? db.Users.First();
                        var acctUser = db.Users.FirstOrDefault(u => u.Username == "accountant") ?? db.Users.First();

                        db.Chats.AddRange(
                            new Chat
                            {
                                SenderId = managerUser.UserId,
                                Message = "สวัสดีทีมงาน Absolute Cinema ทุกท่าน ขอให้ทุกคนเตรียมความพร้อมสำหรับรอบฉายวันนี้ครับ",
                                SentAt = DateTime.Now.AddHours(-3)
                            },
                            new Chat
                            {
                                SenderId = staffUser.UserId,
                                Message = "จุดตรวจตั๋วและเคาน์เตอร์ชั้น 2 พร้อมให้บริการลูกค้าทุกท่านเรียบร้อยแล้วค่ะ",
                                SentAt = DateTime.Now.AddHours(-2)
                            },
                            new Chat
                            {
                                SenderId = techUser.UserId,
                                Message = "ระบบฉายเลเซอร์ 4K และระบบเสียง Dolby Atmos ทุกโรงผ่านการทดสอบพร้อมฉายครับ",
                                SentAt = DateTime.Now.AddHours(-1)
                            },
                            new Chat
                            {
                                SenderId = acctUser.UserId,
                                Message = "ระบบการชำระเงินและระบบคืนเงินออนไลน์เชื่อมต่อกับธนาคารและพร้อมเพย์ทำงานปกติค่ะ",
                                SentAt = DateTime.Now.AddMinutes(-30)
                            }
                        );
                        db.SaveChanges();
                    }

                    _initialized = true;
                }
                catch { }
            }
        }
    }
}
