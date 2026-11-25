using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kawin_67091861_002.Models;
using Kawin_67091861_002.ViewModels;
using Kawin_67091861_002.Helpers;
using static Kawin_67091861_002.Helpers.ChatHelper;

namespace Kawin_67091861_002.Controllers;

public class OperationsController : Controller
{
    private readonly AbsoluteCinemaContext _db;
    private readonly IWebHostEnvironment _env;

    public OperationsController(AbsoluteCinemaContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // Helper: Refresh Badge Counts for View
    private void RefreshBadgeCounts()
    {
        bool incidentsSeen = HttpContext.Session.GetString("Incidents_Seen") == "true";
        int unreadIncidents = incidentsSeen ? 0 : 3;

        int unreadChat = GetTotalUnreadTechChatCount();

        ViewBag.UnreadIncidentsCount = unreadIncidents;
        ViewBag.UnreadChatCount = unreadChat;
    }

    // =========================================================================
    // Auth Guard: ตรวจสิทธิ์ทุก action (Technician หรือ Admin เท่านั้น)
    // =========================================================================
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        base.OnActionExecuting(context);
        if (HttpContext.Session.GetString("UserLoggedIn") != "true")
        {
            context.Result = RedirectToAction("Login", "Account");
            return;
        }
        var role = HttpContext.Session.GetString("UserRole") ?? "";
        if (role != "Technician" && role != "Admin")
        {
            context.Result = RedirectToAction("Index", "Home");
        }
    }

    // =========================================================================
    // 1. ศูนย์ควบคุม (Dashboard Overview)
    // =========================================================================
    public IActionResult Index(string filter = "all")
    {
        RefreshBadgeCounts();
        var vm = new OperationsDashboardViewModel();

        // 1. Alert Banner
        vm.ActiveAlert = new IncidentAlertDto
        {
            Title = "ตรวจพบความผิดปกติ: ระบบเสียงโรง 2 ไม่เสถียร",
            Description = "ค่า Audio Drop สูงกว่ากำหนด 18% • ออกเลขแจ้งเหตุ INC-0842 อัตโนมัติแล้ว",
            TimeAgoText = "4 นาทีที่แล้ว",
            IncidentCode = "INC-0842"
        };

        // 2. KPI Cards
        vm.SystemUptimePercent = 98.7;
        vm.SystemUptimeSubtext = "7 ระบบปกติ";
        vm.OpenIncidentsCount = 6;
        vm.OpenIncidentsSubtext = "เพิ่มขึ้น 1";
        vm.AvgResponseTimeText = "8m 24s";
        vm.AvgResponseTimeSubtext = "เร็วขึ้น 14%";
        vm.OnDutyTechniciansCount = 9;
        vm.OnDutyTechniciansSubtext = "เตรียมพร้อม 5";

        // 3. รายการเหตุขัดข้อง
        var incidents = GetIncidentsFromFile();
        if (filter == "open" || filter == "urgent") incidents = incidents.Where(i => i.Status == "เร่งด่วน" || i.Status == "เปิดอยู่").ToList();
        else if (filter == "investigating") incidents = incidents.Where(i => i.Status == "กำลังตรวจสอบ").ToList();

        vm.Incidents = incidents;

        // 4. สุขภาพระบบ (System Health)
        vm.HealthMetrics = new List<SystemHealthItemDto>
        {
            new() { Name = "ระบบฉายและเสียง", ValueText = "99.99%", Percent = 100, BarColor = "green" },
            new() { Name = "เซิร์ฟเวอร์หลัก", ValueText = "66% load", Percent = 66, BarColor = "green" },
            new() { Name = "เครือข่ายภายใน", ValueText = "24 ms", Percent = 85, BarColor = "green" },
            new() { Name = "ระบบระบายอากาศ", ValueText = "7/8", Percent = 87, BarColor = "orange" }
        };

        // 5. งานบำรุงรักษา (Maintenance Tasks)
        vm.MaintenanceTasks = new List<MaintenanceTaskItemDto>
        {
            new() { Title = "ตรวจเช็ค Projector โรง 4", ScheduleText = "วันนี้ • 13:00", StatusBadge = "ด่วนกำหนด", BadgeType = "teal" },
            new() { Title = "เปลี่ยน Filter ระบบปรับอากาศ", ScheduleText = "วันนี้ • 15:30", StatusBadge = "รอดำเนินการ", BadgeType = "orange" },
            new() { Title = "สำรองฐานข้อมูลประจำวัน", ScheduleText = "พรุ่งนี้ • 23:00", StatusBadge = "(อัตโนมัติ)", BadgeType = "muted" }
        };

        ViewBag.ActiveFilter = filter;
        return View(vm);
    }

    // =========================================================================
    // 2. การจัดการเหตุขัดข้อง (Incidents Management Page - Image 1)
    // =========================================================================
    public IActionResult Incidents(string filter = "all", string? selectedCode = null)
    {
        // เมื่อกดเข้ามาดู ให้มาร์กว่าอ่านแล้ว -> ตัวเลขสีแดงตรง navbar จะหายไป
        HttpContext.Session.SetString("Incidents_Seen", "true");
        RefreshBadgeCounts();

        var allIncidents = GetIncidentsFromFile();

        int total = allIncidents.Count;
        int urgent = allIncidents.Count(i => i.Status == "เร่งด่วน" || i.Status == "เปิดอยู่");
        int investigating = allIncidents.Count(i => i.Status == "กำลังตรวจสอบ");
        int resolved = allIncidents.Count(i => i.Status == "แก้ไขแล้ว" || i.Status == "วางแผนแล้ว" || i.Status == "สำเร็จแล้ว");

        var filteredList = allIncidents;
        if (filter == "urgent")
        {
            filteredList = allIncidents.Where(i => i.Status == "เร่งด่วน" || i.Status == "เปิดอยู่").ToList();
        }
        else if (filter == "investigating")
        {
            filteredList = allIncidents.Where(i => i.Status == "กำลังตรวจสอบ").ToList();
        }
        else if (filter == "resolved")
        {
            filteredList = allIncidents.Where(i => i.Status == "แก้ไขแล้ว" || i.Status == "วางแผนแล้ว" || i.Status == "สำเร็จแล้ว").ToList();
        }

        IncidentItemDto? selected = null;
        if (!string.IsNullOrEmpty(selectedCode))
        {
            selected = allIncidents.FirstOrDefault(i => i.Code.Equals(selectedCode, StringComparison.OrdinalIgnoreCase));
        }

        if (selected == null && filteredList.Any())
        {
            selected = filteredList.First();
        }
        else if (selected == null && allIncidents.Any())
        {
            selected = allIncidents.First();
        }

        var vm = new IncidentsPageViewModel
        {
            Incidents = filteredList,
            SelectedIncident = selected,
            TotalCount = total,
            UrgentCount = urgent,
            InvestigatingCount = investigating,
            ResolvedCount = resolved,
            ActiveFilter = filter
        };

        return View(vm);
    }

    // =========================================================================
    // 3. สุขภาพระบบเทคโนโลยี (System Health Page - Image 2)
    // =========================================================================
    public IActionResult SystemHealth()
    {
        RefreshBadgeCounts();
        var vm = new SystemHealthPageViewModel
        {
            OverallServerStatus = "98.7%",
            OverallServerSubtext = "7 ระบบทำงานปกติ",
            AvgStorageHeat = "42°C",
            AvgStorageHeatSubtext = "ระบบหล่อเย็นปกติ",
            TotalDataToday = "1.8 TB",
            TotalDataSubtext = "อัตราแลกเปลี่ยนปกติ",
            AlertsCount = "2 แจ้งเตือน",
            AlertsSubtext = "สเตตัสพร้อมใช้งาน",
            Subsystems = new List<SubsystemMetricCardDto>
            {
                new()
                {
                    Name = "ระบบจองและชำระเงิน",
                    StatusBadge = "ดีเยี่ยม",
                    BadgeColor = "green",
                    Value = "99.99%",
                    SparklinePoints = "0,35 20,33 40,30 60,32 80,25 100,27 120,20 140,22 160,18 180,15 200,12",
                    LineColor = "#10b981",
                    Subtext = "ทำงานปกติ • Uptime 100% วันนี้อัปเดตเมื่อ 1 นาทีที่แล้ว"
                },
                new()
                {
                    Name = "เซิร์ฟเวอร์สาขา (SRV-Siam)",
                    StatusBadge = "ปกติ",
                    BadgeColor = "green",
                    Value = "68% load",
                    SparklinePoints = "0,25 20,28 40,15 60,30 80,20 100,32 120,18 140,24 160,20 180,15 200,22",
                    LineColor = "#10b981",
                    Subtext = "CPU Temp: 54°C • RAM: 12GB อัปเดตเมื่อ 1 นาทีที่แล้ว"
                },
                new()
                {
                    Name = "เครือข่ายภายในระบบ",
                    StatusBadge = "ดีเยี่ยม",
                    BadgeColor = "green",
                    Value = "24 ms",
                    SparklinePoints = "0,30 20,28 40,29 60,27 80,28 100,25 120,26 140,24 160,25 180,22 200,23",
                    LineColor = "#10b981",
                    Subtext = "Jitter: 1.2ms • Packet loss: 0% อัปเดตเมื่อ 1 นาทีที่แล้ว"
                },
                new()
                {
                    Name = "เครื่องฉายภาพยนตร์ (Projectors)",
                    StatusBadge = "มีปัญหาเบาบาง",
                    BadgeColor = "amber",
                    Value = "7/8 Online",
                    SparklinePoints = "0,20 20,20 40,20 60,25 80,32 100,35 120,30 140,28 160,25 180,24 200,24",
                    LineColor = "#f59e0b",
                    Subtext = "โรง 2 ออฟไลน์เครื่องฉายสำรอง | อัปเดตเมื่อ 1 นาทีที่แล้ว"
                },
                new()
                {
                    Name = "ระบบปรับอากาศ (HVAC)",
                    StatusBadge = "ดีเยี่ยม",
                    BadgeColor = "green",
                    Value = "8/8 Online",
                    SparklinePoints = "0,28 20,27 40,28 60,26 80,27 100,26 120,27 140,25 160,26 180,25 200,25",
                    LineColor = "#10b981",
                    Subtext = "อุณหภูมิโรงเฉลี่ย 21.8°C | อัปเดตเมื่อ 1 นาทีที่แล้ว"
                },
                new()
                {
                    Name = "ระบบไฟฟ้ากำลังหลัก",
                    StatusBadge = "ปกติ",
                    BadgeColor = "green",
                    Value = "ปกติ (220V)",
                    SparklinePoints = "0,25 20,24 40,25 60,25 80,24 100,25 120,25 140,24 160,25 180,25 200,24",
                    LineColor = "#10b981",
                    Subtext = "กำลังสำรอง (UPS) พร้อมใช้งาน 100% อัปเดตเมื่อ 1 นาทีที่แล้ว"
                }
            }
        };

        return View(vm);
    }

    public IActionResult Health() => RedirectToAction("SystemHealth");

    // =========================================================================
    // 4. ตารางงานบำรุงรักษา (Maintenance Schedule - Image 1)
    // =========================================================================
    public IActionResult Maintenance(int day = 15)
    {
        RefreshBadgeCounts();
        var tasks = GetMaintenanceTasksFromFile();

        var vm = new MaintenancePageViewModel
        {
            SelectedDay = day,
            Tasks = tasks,
            PendingCountToday = tasks.Count(t => t.Status != "เสร็จสิ้น"),
            PendingSubtext = "เร่งด่วน 1 งาน",
            CompletedOnTimePercent = "94.2%",
            CompletedOnTimeSubtext = "+2.4% ดีขึ้นจากวีคก่อน",
            AvgDelayMinutes = "12 นาที",
            AvgDelaySubtext = "อยู่ในเกณฑ์ปลอดภัย"
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult CreateMaintenanceTask([FromForm] CreateMaintenanceDto dto)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.Title))
        {
            var tasks = GetMaintenanceTasksFromFile();
            int newId = tasks.Any() ? tasks.Max(t => t.Id) + 1 : 1;

            string catColor = dto.Category == "ฉุกเฉิน" ? "red" : (dto.Category == "แก้ไขทันที" ? "amber" : "blue");
            string statusDot = dto.Category == "ฉุกเฉิน" ? "red" : "amber";

            tasks.Insert(0, new MaintenanceTaskItemDto
            {
                Id = newId,
                Title = $"{dto.Title.Trim()} ({dto.CinemaHall})",
                Category = dto.Category,
                CategoryColor = catColor,
                Assignee = dto.Assignee,
                ScheduleText = dto.ScheduleTime,
                Status = "กำลังดำเนินงาน",
                StatusDotColor = statusDot
            });

            SaveMaintenanceTasksToFile(tasks);
            TempData["Success"] = $"สร้างงานบำรุงรักษา '{dto.Title}' เรียบร้อยแล้ว";
        }

        return RedirectToAction("Maintenance");
    }

    // =========================================================================
    // 5. รายการอุปกรณ์และทรัพย์สิน (Hardware & Assets - Image 2)
    // =========================================================================
    public IActionResult Hardware()
    {
        RefreshBadgeCounts();
        var assets = GetHardwareAssetsFromFile();

        var vm = new HardwarePageViewModel
        {
            TotalAssetsCount = assets.Count,
            TotalAssetsSubtext = "8 โรงหนังครอบคลุมทั้งหมด",
            OnlineStandbyRate = "96.2%",
            OnlineStandbySubtext = $"มีออฟไลน์ชั่วคราว {assets.Count(a => a.Status != "ปกติ")} ชิ้น",
            DueForMaintenanceCount = assets.Count(a => a.IsUrgentMaintenance || a.Status == "ต้องซ่อมบำรุง"),
            DueForMaintenanceSubtext = "กำหนดตรวจสอบรอบเช้านี้",
            Assets = assets
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult CreateHardwareAsset([FromForm] CreateHardwareDto dto)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.NameAndModel))
        {
            var assets = GetHardwareAssetsFromFile();
            int newId = assets.Any() ? assets.Max(a => a.Id) + 1 : 1;

            string color = dto.Status == "ปกติ" ? "green" : (dto.Status == "ต้องซ่อมบำรุง" ? "amber" : "red");

            assets.Insert(0, new HardwareAssetDto
            {
                Id = newId,
                NameAndModel = dto.NameAndModel.Trim(),
                Category = dto.Category,
                CinemaHall = dto.CinemaHall,
                Status = dto.Status,
                StatusColor = color,
                InstalledDate = $"{DateTime.Now.Day} {GetThaiMonthShort(DateTime.Now.Month)} {DateTime.Now.Year + 543}",
                NextMaintenanceDate = $"{DateTime.Now.AddMonths(6).Day} {GetThaiMonthShort(DateTime.Now.AddMonths(6).Month)} {DateTime.Now.AddMonths(6).Year + 543}",
                IsUrgentMaintenance = dto.Status == "ต้องซ่อมบำรุง"
            });

            SaveHardwareAssetsToFile(assets);
            TempData["Success"] = $"ลงทะเบียนอุปกรณ์ '{dto.NameAndModel}' เข้าสู่ฐานระบบเรียบร้อยแล้ว";
        }

        return RedirectToAction("Hardware");
    }

    // =========================================================================
    // 6. บันทึกการส่งต่องานเปลี่ยนเวร (Shift Handover Logs - Image 3)
    // =========================================================================
    public IActionResult ShiftLog()
    {
        RefreshBadgeCounts();
        var logs = GetShiftLogsFromFile();

        var vm = new ShiftLogPageViewModel
        {
            CurrentShiftTitle = "เวรปฏิบัติการปัจจุบัน: เวรบ่าย (ช่างกะสอง)",
            CurrentShiftSubtext = "รับผิดชอบหลักโดย กิตติพงษ์ • สมาชิกร่วมทีมออนไซต์ 3 คน • กำหนดเวลาส่งมอบรอบถัดไป 23:30 น.",
            CurrentShiftDuration = "เข้ากะแล้ว 4 ชม.",
            Logs = logs
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult CreateShiftLog([FromForm] CreateShiftLogDto dto)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.Note))
        {
            var logs = GetShiftLogsFromFile();
            int newId = logs.Any() ? logs.Max(l => l.Id) + 1 : 1;

            logs.Insert(0, new ShiftHandoverLogDto
            {
                Id = newId,
                Title = dto.ShiftTitle,
                SenderName = dto.SenderName,
                ReceiverName = dto.ReceiverName,
                TimeText = $"วันนี้ • {DateTime.Now:HH:mm} น.",
                Note = dto.Note.Trim(),
                StatusBadge = dto.HasUrgentPending ? "มีงานด่วนค้างคา" : "",
                DotColor = dto.HasUrgentPending ? "yellow" : "green"
            });

            SaveShiftLogsToFile(logs);
            TempData["Success"] = "บันทึกการส่งมอบเวรเรียบร้อยแล้ว";
        }

        return RedirectToAction("ShiftLog");
    }

    // =========================================================================
    // 7. ระบบแชต (แชทกลุ่มรวมทุกแผนก)
    // =========================================================================
    public IActionResult TechChat(string channel = "group")
    {
        int myUserId = ChatHelper.GetCurrentUserId(HttpContext, "Technician");
        string activeRoom = "group";

        HttpContext.Session.SetString("TechChat_Seen_group", "true");
        RefreshBadgeCounts();

        string myName = HttpContext.Session.GetString("UserName") ?? "ช่างเทคนิค";

        var rooms = new List<TechChatRoomDto>
        {
            new()
            {
                Id = "group",
                Name = "แชทกลุ่มรวม (All Staff)",
                LastMessageTime = "",
                LastMessageText = "สื่อสารระหว่างพนักงานทุกแผนกในสาขา",
                UnreadCount = 0,
                IsActive = true,
                OnlineStatusText = "ผู้จัดการ, พนักงานบริการ, ช่างเทคนิค, บัญชี, แอดมิน"
            }
        };

        var activeRoomDto = rooms.First();

        string role = HttpContext.Session.GetString("UserRole") ?? "Technician";
        var roomMessages = ChatHelper.GetMessagesForChannel(_db, _env, activeRoom, role, myName, myUserId)
            .Select(m => new TechChatMessageDto
            {
                Id = m.Id,
                ChannelId = m.ChannelId,
                SenderName = m.SenderName,
                TimeText = m.TimeText,
                MessageText = m.MessageText,
                IsMine = m.IsMine,
                CreatedAt = m.CreatedAt
            })
            .ToList();

        var vm = new TechChatPageViewModel
        {
            ActiveRoomId = activeRoom,
            ActiveRoom = activeRoomDto,
            Rooms = rooms,
            Messages = roomMessages,
            TotalUnreadCount = 0
        };

        return View(vm);
    }

    [HttpPost]
    public IActionResult SendTechChatMessage([FromForm] string channelId, [FromForm] string messageText)
    {
        if (!string.IsNullOrWhiteSpace(messageText))
        {
            string sender = HttpContext.Session.GetString("UserName") ?? "ช่างเทคนิค";
            string role = HttpContext.Session.GetString("UserRole") ?? "Technician";
            ChatHelper.SendMessage(_db, _env, "group", messageText, sender, role, HttpContext);

            HttpContext.Session.SetString("TechChat_Seen_group", "true");
            return RedirectToAction("TechChat", new { channel = "group" });
        }
        return RedirectToAction("TechChat");
    }

    [HttpGet]
    public IActionResult GetMessagesApi(string channel = "group")
    {
        int myUserId = ChatHelper.GetCurrentUserId(HttpContext, "Technician");
        string role = HttpContext.Session.GetString("UserRole") ?? "Technician";
        string myName = HttpContext.Session.GetString("UserName") ?? "ช่างเทคนิค";
        var messages = ChatHelper.GetMessagesForChannel(_db, _env, "group", role, myName, myUserId);
        return Json(new { success = true, channel = "group", messages = messages });
    }

    [HttpPost]
    public IActionResult SendMessageApi([FromForm] string channelId, [FromForm] string messageText)
    {
        if (string.IsNullOrWhiteSpace(messageText))
            return Json(new { success = false, message = "ข้อความว่างเปล่า" });

        string sender = HttpContext.Session.GetString("UserName") ?? "ช่างเทคนิค";
        string role = HttpContext.Session.GetString("UserRole") ?? "Technician";
        var newMsg = ChatHelper.SendMessage(_db, _env, "group", messageText, sender, role, HttpContext);

        HttpContext.Session.SetString("TechChat_Seen_group", "true");
        return Json(new { success = true, newMsg = newMsg });
    }

    // =========================================================================
    // Actions for Incident Management
    // =========================================================================
    [HttpPost]
    public IActionResult CreateIncident([FromForm] CreateIncidentDto dto)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.Title))
        {
            var incidents = GetIncidentsFromFile();
            string newCode = $"INC-0{new Random().Next(845, 899)}";

            string status = dto.Urgency == "เร่งด่วน" ? "เร่งด่วน" : "กำลังตรวจสอบ";
            string color = dto.Urgency == "เร่งด่วน" ? "red" : "yellow";

            var item = new IncidentItemDto
            {
                Code = newCode,
                Title = dto.Title.Trim(),
                SourceAndTime = $"{dto.CinemaHall} • {DateTime.Now:HH:mm}",
                Assignee = string.IsNullOrWhiteSpace(dto.Assignee) ? "อนุชา" : dto.Assignee,
                Status = status,
                StatusColor = color,
                UrgencyLevel = dto.Urgency,
                OpenTime = DateTime.Now.ToString("HH:mm"),
                Detector = string.IsNullOrWhiteSpace(dto.Detector) ? "เจ้าหน้าที่ฝ่ายเทคนิค" : dto.Detector,
                TimeAgoText = "เพิ่งแจ้งเมื่อสักครู่",
                ReportedAt = DateTime.Now,
                ActivityLogs = new List<IncidentActivityLogDto>
                {
                    new()
                    {
                        TimeText = DateTime.Now.ToString("HH:mm"),
                        Title = $"เปิดรับแจ้งเหตุขัดข้อง ({dto.Urgency})",
                        Description = string.IsNullOrWhiteSpace(dto.Description) ? "กำลังประสานงานช่างเข้าตรวจสอบจุดเกิดเหตุ" : dto.Description,
                        Color = color
                    }
                }
            };

            incidents.Insert(0, item);
            SaveIncidentsToFile(incidents);

            // เมื่อมีแจ้งเตือนใหม่เข้ามา ให้เคลียร์ Seen เพื่อให้ Badge สีแดงเด้งขึ้นมาใหม่
            HttpContext.Session.Remove("Incidents_Seen");

            try
            {
                if (_db.Database.CanConnect())
                {
                    _db.Operations.Add(new Operation
                    {
                        CinemaId = 1,
                        ReportedBy = 1,
                        TaskType = "Incident",
                        Description = $"{item.Code}: {item.Title} ({dto.Description})",
                        Status = item.Status
                    });
                    _db.SaveChanges();
                }
            }
            catch { }

            TempData["Success"] = $"สร้างรายการแจ้งเหตุ {item.Code} เรียบร้อยแล้ว ระบบกระจายงานไปยังช่างเทคนิคทันที";
            return RedirectToAction("Incidents", new { selectedCode = newCode });
        }

        return RedirectToAction("Incidents");
    }

    [HttpPost]
    public IActionResult UpdateIncidentResult([FromForm] UpdateIncidentResultDto dto)
    {
        if (dto != null && !string.IsNullOrEmpty(dto.Code))
        {
            var incidents = GetIncidentsFromFile();
            var inc = incidents.FirstOrDefault(i => i.Code.Equals(dto.Code, StringComparison.OrdinalIgnoreCase));
            if (inc != null)
            {
                if (!string.IsNullOrWhiteSpace(dto.NewStatus))
                {
                    inc.Status = dto.NewStatus;
                    inc.StatusColor = dto.NewStatus switch
                    {
                        "แก้ไขแล้ว" => "green",
                        "วางแผนแล้ว" => "teal",
                        "กำลังตรวจสอบ" => "yellow",
                        _ => "red"
                    };
                }

                if (!string.IsNullOrWhiteSpace(dto.ActivityTitle))
                {
                    inc.ActivityLogs.Add(new IncidentActivityLogDto
                    {
                        TimeText = DateTime.Now.ToString("HH:mm"),
                        Title = dto.ActivityTitle.Trim(),
                        Description = dto.ActivityDescription?.Trim() ?? "บันทึกผลการตรวจสอบจากฝ่ายเทคนิค",
                        Color = inc.StatusColor
                    });
                }

                SaveIncidentsToFile(incidents);

                try
                {
                    if (_db.Database.CanConnect())
                    {
                        var op = _db.Operations.FirstOrDefault(o => o.Description != null && o.Description.Contains(inc.Code));
                        if (op != null)
                        {
                            op.Status = inc.Status;
                            _db.SaveChanges();
                        }
                    }
                }
                catch { }

                TempData["Success"] = $"บันทึกผลการตรวจสอบสำหรับ {inc.Code} เรียบร้อยแล้ว (สถานะ: {inc.Status})";
                return RedirectToAction("Incidents", new { selectedCode = inc.Code });
            }
        }

        return RedirectToAction("Incidents");
    }

    [HttpPost]
    public IActionResult EscalateToManager(string code)
    {
        if (!string.IsNullOrEmpty(code))
        {
            var incidents = GetIncidentsFromFile();
            var inc = incidents.FirstOrDefault(i => i.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (inc != null)
            {
                inc.ActivityLogs.Add(new IncidentActivityLogDto
                {
                    TimeText = DateTime.Now.ToString("HH:mm"),
                    Title = "ส่งต่อเรื่องให้ผู้จัดการสาขารับทราบ (Escalated)",
                    Description = "แจ้งเตือนระดับ Priority สูงไปยังห้องควบคุมของผู้จัดการสาขาเรียบร้อยแล้ว",
                    Color = "red"
                });

                SaveIncidentsToFile(incidents);

                // ส่งเข้าแชทข้าม Role (ผู้จัดการ)
                try
                {
                    var allMessages = GetChatMessagesFromFile();
                    allMessages.Add(new ChatMessageStoreDto
                    {
                        Id = allMessages.Any() ? allMessages.Max(m => m.Id) + 1 : 1,
                        ChannelId = "manager",
                        SenderName = "กิตติพงษ์ (หัวหน้าช่าง)",
                        TimeText = DateTime.Now.ToString("HH:mm"),
                        MessageText = $"[ด่วนมาก] ส่งต่อเหตุขัดข้อง {inc.Code}: {inc.Title} เพื่อพิจารณาและอนุมัติการดำเนินงาน",
                        IsMine = true,
                        CreatedAt = DateTime.Now
                    });
                    SaveChatMessagesToFile(allMessages);
                }
                catch { }

                TempData["Success"] = $"ส่งต่อเรื่อง {inc.Code} ไปยังผู้จัดการสาขาเรียบร้อยแล้ว ระบบแจ้งเตือนไปยังทีมบริหารทันที";
                return RedirectToAction("Incidents", new { selectedCode = inc.Code });
            }
        }

        return RedirectToAction("Incidents");
    }

    // =========================================================================
    // Helper Methods: Calculation of Unread Badges & Storage
    // =========================================================================
    private int GetTotalUnreadTechChatCount()
    {
        int count = 0;
        if (HttpContext.Session.GetString("TechChat_Seen_group") != "true") count += 2;
        if (HttpContext.Session.GetString("TechChat_Seen_admin") != "true") count += 1;
        return count;
    }

    private List<TechChatRoomDto> GetTechChatRooms(string activeRoom, List<ChatMessageStoreDto> allMessages)
    {
        int oncallUnread = HttpContext.Session.GetString("TechChat_Seen_tech-oncall") == "true" ? 0 : 2;
        int managerUnread = HttpContext.Session.GetString("TechChat_Seen_manager") == "true" ? 0 : 1;
        int facilityUnread = HttpContext.Session.GetString("TechChat_Seen_facility") == "true" ? 0 : 0;

        return new List<TechChatRoomDto>
        {
            new()
            {
                Id = "tech-ops",
                Name = "กลุ่มช่างปฏิบัติการหลัก",
                LastMessageTime = "10:48",
                LastMessageText = "กิตติพงษ์: มีการอัปเดตเซิร์ฟเวอร์",
                UnreadCount = 0,
                IsActive = activeRoom == "tech-ops",
                OnlineStatusText = "ออนไลน์ 4 คนในสาขา"
            },
            new()
            {
                Id = "tech-oncall",
                Name = "เวรรอบดึก (On-Call)",
                LastMessageTime = "10:45",
                LastMessageText = "อนุชา: เข้าโรงภาพยนตร์ 2 เรียบร้อยครับ",
                UnreadCount = oncallUnread,
                IsActive = activeRoom == "tech-oncall",
                OnlineStatusText = "สแตนด์บาย 2 คน"
            },
            new()
            {
                Id = "manager",
                Name = "ผู้จัดการสาขา ณัฐชา",
                LastMessageTime = "10:30",
                LastMessageText = "ณัฐชา: ระบบเสียงโรง 2 เรียบร้อยยังคะ?",
                UnreadCount = managerUnread,
                IsActive = activeRoom == "manager",
                OnlineStatusText = "ออนไลน์ (ห้องผู้จัดการสาขา)"
            },
            new()
            {
                Id = "facility",
                Name = "ฝ่ายบำรุงรักษาอาคาร",
                LastMessageTime = "09:12",
                LastMessageText = "วรพล: เปลี่ยนแผ่นกรองเสร็จตามเวลาครับ",
                UnreadCount = facilityUnread,
                IsActive = activeRoom == "facility",
                OnlineStatusText = "ออนไลน์ 3 คน"
            }
        };
    }

    private List<IncidentItemDto> GetIncidentsFromFile()
    {
        var list = new List<IncidentItemDto>();
        try
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "operations_incidents.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<IncidentItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null) list = items;
            }
        }
        catch { }
        return list;
    }

    private void SaveIncidentsToFile(List<IncidentItemDto> items)
    {
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, "operations_incidents.json");
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }
        catch { }
    }

    private List<MaintenanceTaskItemDto> GetMaintenanceTasksFromFile()
    {
        var list = new List<MaintenanceTaskItemDto>();
        try
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "operations_maintenance.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<MaintenanceTaskItemDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null) list = items;
            }
        }
        catch { }
        return list;
    }

    private void SaveMaintenanceTasksToFile(List<MaintenanceTaskItemDto> items)
    {
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, "operations_maintenance.json");
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }
        catch { }
    }

    private List<HardwareAssetDto> GetHardwareAssetsFromFile()
    {
        var list = new List<HardwareAssetDto>();
        try
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "operations_hardware.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<HardwareAssetDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null) list = items;
            }
        }
        catch { }
        return list;
    }

    private void SaveHardwareAssetsToFile(List<HardwareAssetDto> items)
    {
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, "operations_hardware.json");
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }
        catch { }
    }

    private List<ShiftHandoverLogDto> GetShiftLogsFromFile()
    {
        var list = new List<ShiftHandoverLogDto>();
        try
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "operations_shiftlogs.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<ShiftHandoverLogDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null) list = items;
            }
        }
        catch { }
        return list;
    }

    private void SaveShiftLogsToFile(List<ShiftHandoverLogDto> items)
    {
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, "operations_shiftlogs.json");
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }
        catch { }
    }

    private List<ChatMessageStoreDto> GetChatMessagesFromFile()
    {
        var list = new List<ChatMessageStoreDto>();
        try
        {
            var path = Path.Combine(_env.ContentRootPath, "Data", "chat_messages.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<ChatMessageStoreDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null) list = items;
            }
        }
        catch { }
        return list;
    }

    private void SaveChatMessagesToFile(List<ChatMessageStoreDto> items)
    {
        try
        {
            var dataDir = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var filePath = Path.Combine(dataDir, "chat_messages.json");
            var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(filePath, json);
        }
        catch { }
    }

    private string GetThaiMonthShort(int month) => month switch
    {
        1 => "ม.ค.", 2 => "ก.พ.", 3 => "มี.ค.", 4 => "เม.ย.",
        5 => "พ.ค.", 6 => "มิ.ย.", 7 => "ก.ค.", 8 => "ส.ค.",
        9 => "ก.ย.", 10 => "ต.ค.", 11 => "พ.ย.", 12 => "ธ.ค.",
        _ => ""
    };

    private class ChatMessageStoreDto
    {
        public int Id { get; set; }
        public string ChannelId { get; set; } = "";
        public string SenderName { get; set; } = "";
        public string TimeText { get; set; } = "";
        public string MessageText { get; set; } = "";
        public bool IsMine { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
