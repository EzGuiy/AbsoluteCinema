using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.ViewModels;

public class OperationsDashboardViewModel
{
    // 4 KPI Stats
    public double SystemUptimePercent { get; set; } = 98.7;
    public string SystemUptimeSubtext { get; set; } = "7 ระบบปกติ";

    public int OpenIncidentsCount { get; set; } = 6;
    public string OpenIncidentsSubtext { get; set; } = "เพิ่มขึ้น 1";

    public string AvgResponseTimeText { get; set; } = "8m 24s";
    public string AvgResponseTimeSubtext { get; set; } = "เร็วขึ้น 14%";

    public int OnDutyTechniciansCount { get; set; } = 9;
    public string OnDutyTechniciansSubtext { get; set; } = "เตรียมพร้อม 5";

    // Active Banner Alert
    public IncidentAlertDto ActiveAlert { get; set; } = new();

    // Incidents list
    public List<IncidentItemDto> Incidents { get; set; } = new();

    // System Health Metrics
    public List<SystemHealthItemDto> HealthMetrics { get; set; } = new();

    // Maintenance Tasks
    public List<MaintenanceTaskItemDto> MaintenanceTasks { get; set; } = new();
}

public class IncidentAlertDto
{
    public string Title { get; set; } = "ตรวจพบความผิดปกติ: ระบบเสียงโรง 2 ไม่เสถียร";
    public string Description { get; set; } = "ค่า Audio Drop สูงกว่ากำหนด 18% • ออกเลขแจ้งเหตุ INC-0842 อัตโนมัติแล้ว";
    public string TimeAgoText { get; set; } = "4 นาทีที่แล้ว";
    public string IncidentCode { get; set; } = "INC-0842";
}

public class IncidentItemDto
{
    public string Code { get; set; } = ""; // INC-0842
    public string Title { get; set; } = ""; // เสียงกระตุกในโรง 2
    public string SourceAndTime { get; set; } = ""; // ระบบตรวจพบอัตโนมัติ • 10:42
    public string Assignee { get; set; } = ""; // อนุชา
    public string Status { get; set; } = "เปิดอยู่"; // เปิดอยู่, กำลังตรวจสอบ, สำเร็จแล้ว, รายงานแล้ว
    public string StatusColor { get; set; } = "red"; // red, yellow, teal, green
    public DateTime ReportedAt { get; set; } = DateTime.Now;
}

public class SystemHealthItemDto
{
    public string Name { get; set; } = "";
    public string ValueText { get; set; } = "";
    public int Percent { get; set; }
    public string BarColor { get; set; } = "green"; // green, orange, red
}

public class MaintenanceTaskItemDto
{
    public string Title { get; set; } = ""; // ตรวจเช็ค Projector โรง 4
    public string ScheduleText { get; set; } = ""; // วันนี้ • 13:00
    public string StatusBadge { get; set; } = ""; // ด่วนกำหนด, รอดำเนินการ, (อัตโนมัติ)
    public string BadgeType { get; set; } = "teal"; // teal, orange, muted
}

public class CreateIncidentDto
{
    public string Title { get; set; } = "";
    public string CinemaHall { get; set; } = "โรง 1";
    public string Assignee { get; set; } = "อนุชา";
    public string Description { get; set; } = "";
    public string Severity { get; set; } = "สูง";
}

