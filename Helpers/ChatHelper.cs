using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Kawin_67091861_002.Models;
using Kawin_67091861_002.ViewModels;

namespace Kawin_67091861_002.Helpers
{
    public static class ChatHelper
    {
        private static readonly object _lock = new();

        public static void EnsureDatabaseChatReady(AbsoluteCinemaContext db, IWebHostEnvironment env)
        {
            DbInitializer.Initialize(db, env.ContentRootPath);
        }

        public static List<ManagerChatMessageDto> GetMessagesForChannel(
            AbsoluteCinemaContext db,
            IWebHostEnvironment env,
            string activeChannel,
            string currentRoleName,
            string? currentUserName = null,
            int currentUserId = 0)
        {
            var result = new List<ManagerChatMessageDto>();
            try
            {
                if (db.Database.CanConnect())
                {
                    EnsureDatabaseChatReady(db, env);

                    var chats = db.Chats
                        .Include(c => c.Sender).ThenInclude(u => u.Role)
                        .OrderBy(c => c.SentAt)
                        .ToList();

                    foreach (var c in chats)
                    {
                        string senderRole = c.Sender?.Role?.RoleName ?? "Staff";
                        string roleBadge = senderRole switch
                        {
                            "Admin" => "ผู้ดูแลระบบ",
                            "Manager" => "ผู้จัดการสาขา",
                            "Staff" => "พนักงานบริการ",
                            "Technician" => "ช่างเทคนิค",
                            "Accountant" => "ฝ่ายบัญชี",
                            _ => senderRole
                        };

                        string baseName = c.Sender != null ? (c.Sender.FullName ?? c.Sender.Username) : "ทีมงาน";
                        if (!string.IsNullOrEmpty(roleBadge) && !baseName.Contains(roleBadge) && !baseName.Contains("("))
                        {
                            baseName = $"{baseName} ({roleBadge})";
                        }

                        string time = c.SentAt.HasValue ? c.SentAt.Value.ToString("HH:mm น.") : "12:00 น.";
                        bool isMine = false;
                        if (currentUserId > 0 && c.SenderId == currentUserId)
                        {
                            isMine = true;
                        }
                        else if (!string.IsNullOrEmpty(currentUserName) && baseName.Contains(currentUserName))
                        {
                            isMine = true;
                        }

                        result.Add(new ManagerChatMessageDto
                        {
                            Id = c.ChatId,
                            ChannelId = "group",
                            SenderName = baseName,
                            TimeText = time,
                            MessageText = c.Message ?? "",
                            IsMine = isMine,
                            CreatedAt = c.SentAt ?? DateTime.Now
                        });
                    }

                    return result;
                }
            }
            catch { }

            // Fallback from Data/chat_messages.json
            return ReadMessagesFromJson(env);
        }

        public static ManagerChatMessageDto SendMessage(
            AbsoluteCinemaContext db,
            IWebHostEnvironment env,
            string channel,
            string message,
            string senderRoleName,
            string defaultSenderName,
            HttpContext? httpContext = null)
        {
            var now = DateTime.Now;
            string timeText = now.ToString("HH:mm น.");
            string cleanMsg = (message ?? "").Trim();
            int generatedId = 0;

            int senderUid = GetCurrentUserId(httpContext, senderRoleName);
            string sessionUserName = httpContext?.Session.GetString("UserName") ?? defaultSenderName;
            string sessionEmail = httpContext?.Session.GetString("UserEmail") ?? "";

            // 1. บันทึกลง MySQL Database 100%
            try
            {
                if (db.Database.CanConnect())
                {
                    EnsureDatabaseChatReady(db, env);

                    var sender = db.Users.FirstOrDefault(u => u.UserId == senderUid);
                    if (sender == null && !string.IsNullOrEmpty(sessionEmail))
                    {
                        sender = db.Users.FirstOrDefault(u => u.Username.Equals(sessionEmail, StringComparison.OrdinalIgnoreCase));
                    }
                    if (sender == null)
                    {
                        sender = db.Users.FirstOrDefault(u => u.Role != null && u.Role.RoleName.Equals(senderRoleName, StringComparison.OrdinalIgnoreCase))
                                 ?? db.Users.FirstOrDefault();
                    }

                    int resolvedSenderId = sender?.UserId ?? senderUid;
                    if (resolvedSenderId <= 0) resolvedSenderId = 1;

                    var chatEntity = new Chat
                    {
                        SenderId = resolvedSenderId,
                        TargetRole = null, // แชทรวมส่งหาทุกคน
                        Message = cleanMsg,
                        SentAt = now
                    };

                    db.Chats.Add(chatEntity);
                    db.SaveChanges();
                    generatedId = chatEntity.ChatId;

                    if (sender != null)
                    {
                        sessionUserName = sender.FullName ?? sender.Username;
                    }
                }
            }
            catch { }

            // 2. ซิงค์สำรองไฟล์ JSON ด้วย
            var allJsonMsgs = ReadMessagesFromJson(env);
            if (generatedId == 0)
            {
                generatedId = allJsonMsgs.Any() ? allJsonMsgs.Max(m => m.Id) + 1 : 1;
            }

            var newDto = new ManagerChatMessageDto
            {
                Id = generatedId,
                ChannelId = "group",
                SenderName = sessionUserName,
                TimeText = timeText,
                MessageText = cleanMsg,
                IsMine = true,
                CreatedAt = now
            };

            allJsonMsgs.Add(newDto);
            WriteMessagesToJson(env, allJsonMsgs);

            return newDto;
        }

        public static List<ManagerChatRoomDto> GetAdminRooms(
            AbsoluteCinemaContext db,
            IWebHostEnvironment env,
            string activeRoomId,
            HttpContext? httpContext = null)
        {
            return new List<ManagerChatRoomDto>
            {
                new()
                {
                    Id = "group",
                    Name = "แชททีมงานรวม (Team Group Chat)",
                    LastMessageText = "ศูนย์รวมการติดต่อสื่อสารของทีมงาน Absolute Cinema",
                    UnreadCount = 0,
                    ColorType = "blue",
                    IsActive = true,
                    MembersInfo = "ผู้จัดการ, พนักงานบริการ, ช่างเทคนิค, บัญชี, แอดมิน"
                }
            };
        }

        public static List<ManagerChatRoomDto> GetDepartmentRooms(
            AbsoluteCinemaContext db,
            IWebHostEnvironment env,
            string activeChannelId,
            string roleName,
            string roleTitleTh,
            HttpContext? httpContext = null)
        {
            return new List<ManagerChatRoomDto>
            {
                new()
                {
                    Id = "group",
                    Name = "แชททีมงานรวม (Team Group Chat)",
                    LastMessageText = "พูดคุยและประสานงานร่วมกันทุกฝ่าย",
                    UnreadCount = 0,
                    ColorType = "blue",
                    IsActive = true,
                    MembersInfo = "ผู้จัดการ, พนักงานบริการ, ช่างเทคนิค, บัญชี, แอดมิน"
                }
            };
        }

        public static int GetCurrentUserId(HttpContext? httpContext, string roleName = "")
        {
            if (httpContext != null)
            {
                var sUidStr = httpContext.Session.GetString("UserId");
                if (int.TryParse(sUidStr, out int parsedUid) && parsedUid > 0)
                {
                    return parsedUid;
                }
            }

            return (roleName ?? "").ToLowerInvariant() switch
            {
                "admin" => 1,
                "manager" => 2,
                "staff" => 3,
                "technician" => 4,
                "accountant" => 5,
                _ => 1
            };
        }

        public static List<ManagerChatMessageDto> ReadMessagesFromJson(IWebHostEnvironment env)
        {
            try
            {
                var filePath = Path.Combine(env.ContentRootPath, "Data", "chat_messages.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var list = JsonSerializer.Deserialize<List<ManagerChatMessageDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (list != null) return list;
                }
            }
            catch { }
            return new List<ManagerChatMessageDto>();
        }

        private static void WriteMessagesToJson(IWebHostEnvironment env, List<ManagerChatMessageDto> messages)
        {
            try
            {
                var dataDir = Path.Combine(env.ContentRootPath, "Data");
                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
                var filePath = Path.Combine(dataDir, "chat_messages.json");
                var json = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch { }
        }
    }
}
