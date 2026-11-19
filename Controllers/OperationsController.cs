using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Kawin_67091861_002.Models;
using Kawin_67091861_002.ViewModels;

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

    public IActionResult Index(string filter = "all")
    {
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
        if (filter == "open") incidents = incidents.Where(i => i.Status == "เปิดอยู่").ToList();
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

    [HttpPost]
    public IActionResult CreateIncident([FromForm] CreateIncidentDto dto)
    {
        if (dto != null && !string.IsNullOrWhiteSpace(dto.Title))
        {
            var incidents = GetIncidentsFromFile();
            string newCode = $"INC-0{new Random().Next(843, 899)}";

            var item = new IncidentItemDto
            {
                Code = newCode,
                Title = dto.Title.Trim(),
                SourceAndTime = $"{dto.CinemaHall} • {DateTime.Now:HH:mm}",
                Assignee = string.IsNullOrWhiteSpace(dto.Assignee) ? "อนุชา" : dto.Assignee,
                Status = "เปิดอยู่",
                StatusColor = "red",
                ReportedAt = DateTime.Now
            };

            incidents.Insert(0, item);
            SaveIncidentsToFile(incidents);

            // บันทึกลง MySQL Operations table ถ้ามี
            try
            {
                if (_db.Database.CanConnect())
                {
                    _db.Operations.Add(new Operation
                    {
                        CinemaId = 1,
                        ReportedBy = 1,
                        TaskType = "Incident",
                        Description = $"{item.Code}: {item.Title}",
                        Status = item.Status
                    });
                    _db.SaveChanges();
                }
            }
            catch { }

            TempData["Success"] = $"สร้างรายการแจ้งเหตุ {item.Code} เรียบร้อยแล้ว";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult ResolveIncident(string code)
    {
        if (!string.IsNullOrEmpty(code))
        {
            var incidents = GetIncidentsFromFile();
            var inc = incidents.FirstOrDefault(i => i.Code == code);
            if (inc != null)
            {
                inc.Status = "สำเร็จแล้ว";
                inc.StatusColor = "teal";
                SaveIncidentsToFile(incidents);
                TempData["Success"] = $"อัปเดตสถานะ {code} เป็น 'สำเร็จแล้ว' เรียบร้อย";
            }
        }
        return RedirectToAction("Index");
    }

    public IActionResult Health() => RedirectToAction("Index");
    public IActionResult Maintenance() => RedirectToAction("Index");
    public IActionResult Hardware() => RedirectToAction("Index");
    public IActionResult ShiftLog() => RedirectToAction("Index");
    public IActionResult TechChat() => RedirectToAction("Index");

    // ==========================================
    // Helper Methods
    // ==========================================
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

        if (!list.Any())
        {
            list = new List<IncidentItemDto>
            {
                new() { Code = "INC-0842", Title = "เสียงกระตุกในโรง 2", SourceAndTime = "ระบบตรวจพบอัตโนมัติ • 10:42", Assignee = "อนุชา", Status = "เปิดอยู่", StatusColor = "red" },
                new() { Code = "INC-0841", Title = "อุณหภูมิห้อง Server สูง", SourceAndTime = "Sensor SRV-01 • 10:18", Assignee = "วรพล", Status = "กำลังตรวจสอบ", StatusColor = "yellow" },
                new() { Code = "INC-0839", Title = "เครื่องออกตั๋วช่อง 4 ออฟไลน์", SourceAndTime = "พนักงาน Box Office • 09:55", Assignee = "ศุภชัย", Status = "สำเร็จแล้ว", StatusColor = "teal" },
                new() { Code = "INC-0837", Title = "Projector Lamp ใกล้หมดอายุ", SourceAndTime = "โรง 4 • 08:30", Assignee = "กัมปนาท", Status = "รายงานแล้ว", StatusColor = "green" }
            };
            SaveIncidentsToFile(list);
        }

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
}

