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

public class IncidentActivityLogDto
{
    public string TimeText { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Color { get; set; } = "yellow"; // red, yellow, teal, green
}

public class IncidentItemDto
{
    public string Code { get; set; } = ""; // INC-0842
    public string Title { get; set; } = ""; // เสียงขาดช่วงในโรง 2
    public string SourceAndTime { get; set; } = ""; // ระบบตรวจจับอัตโนมัติ • 10:42
    public string Assignee { get; set; } = ""; // อนุชา
    public string Status { get; set; } = "เร่งด่วน"; // เร่งด่วน, กำลังตรวจสอบ, วางแผนแล้ว, แก้ไขแล้ว
    public string StatusColor { get; set; } = "red"; // red, yellow, teal, green
    public string UrgencyLevel { get; set; } = "เร่งด่วนที่สุด";
    public string OpenTime { get; set; } = "10:42";
    public string Detector { get; set; } = "ระบบวิเคราะห์ความถี่เสียงอัตโนมัติ";
    public string TimeAgoText { get; set; } = "4 นาทีที่แล้ว";
    public DateTime ReportedAt { get; set; } = DateTime.Now;
    public List<IncidentActivityLogDto> ActivityLogs { get; set; } = new();
}

public class IncidentsPageViewModel
{
    public List<IncidentItemDto> Incidents { get; set; } = new();
    public IncidentItemDto? SelectedIncident { get; set; }
    public int TotalCount { get; set; }
    public int UrgentCount { get; set; }
    public int InvestigatingCount { get; set; }
    public int ResolvedCount { get; set; }
    public string ActiveFilter { get; set; } = "all";
}

public class SystemHealthItemDto
{
    public string Name { get; set; } = "";
    public string ValueText { get; set; } = "";
    public int Percent { get; set; }
    public string BarColor { get; set; } = "green"; // green, orange, red
}

public class SubsystemMetricCardDto
{
    public string Name { get; set; } = "";
    public string StatusBadge { get; set; } = "ดีเยี่ยม";
    public string BadgeColor { get; set; } = "green"; // green, teal, amber, red
    public string Value { get; set; } = "";
    public string SparklinePoints { get; set; } = "";
    public string LineColor { get; set; } = "#10b981";
    public string Subtext { get; set; } = "";
}

public class SystemHealthPageViewModel
{
    public string OverallServerStatus { get; set; } = "98.7%";
    public string OverallServerSubtext { get; set; } = "7 ระบบทำงานปกติ";
    public string AvgStorageHeat { get; set; } = "42°C";
    public string AvgStorageHeatSubtext { get; set; } = "ระบบหล่อเย็นปกติ";
    public string TotalDataToday { get; set; } = "1.8 TB";
    public string TotalDataSubtext { get; set; } = "อัตราแลกเปลี่ยนปกติ";
    public string AlertsCount { get; set; } = "2 แจ้งเตือน";
    public string AlertsSubtext { get; set; } = "สเตตัสพร้อมใช้งาน";

    public List<SubsystemMetricCardDto> Subsystems { get; set; } = new();
}

public class MaintenanceTaskItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = ""; // เปลี่ยนหลอดไฟโปรเจกเตอร์ โรง 4
    public string Category { get; set; } = "ตามแผน"; // ตามแผน, แก้ไขทันที, ฉุกเฉิน
    public string CategoryColor { get; set; } = "blue"; // blue, amber, red
    public string Assignee { get; set; } = ""; // อนุชา • ช่างเทคนิค
    public string ScheduleText { get; set; } = ""; // วันนี้ • 13:00 น.
    public string Status { get; set; } = "กำลังดำเนินงาน"; // กำลังดำเนินงาน, รอดำเนินการ, เสร็จสิ้น, ระงับชั่วคราว, วางแผนแล้ว
    public string StatusDotColor { get; set; } = "amber"; // amber, grey, green, red, teal
    public string StatusBadge { get; set; } = ""; 
    public string BadgeType { get; set; } = "teal";
}

public class MaintenancePageViewModel
{
    public int PendingCountToday { get; set; } = 4;
    public string PendingSubtext { get; set; } = "เร่งด่วน 1 งาน";
    public string CompletedOnTimePercent { get; set; } = "94.2%";
    public string CompletedOnTimeSubtext { get; set; } = "+2.4% ดีขึ้นจากวีคก่อน";
    public string AvgDelayMinutes { get; set; } = "12 นาที";
    public string AvgDelaySubtext { get; set; } = "อยู่ในเกณฑ์ปลอดภัย";

    public int SelectedDay { get; set; } = 15;
    public List<MaintenanceTaskItemDto> Tasks { get; set; } = new();
}

public class HardwareAssetDto
{
    public int Id { get; set; }
    public string NameAndModel { get; set; } = ""; // Laser Projector 4K - Sony
    public string Category { get; set; } = ""; // ระบบฉายภาพ, ระบบเสียง, เซิร์ฟเวอร์, ระบบปรับอากาศ
    public string CinemaHall { get; set; } = ""; // โรง 1, โรง 2, ห้องเซิร์ฟเวอร์หลัก
    public string Status { get; set; } = "ปกติ"; // ปกติ, ต้องซ่อมบำรุง, ออฟไลน์
    public string StatusColor { get; set; } = "green"; // green, amber, red
    public string InstalledDate { get; set; } = ""; // 12 ม.ค. 2567
    public string NextMaintenanceDate { get; set; } = ""; // 12 ต.ค. 2569, วันนี้, ระงับชั่วคราว
    public bool IsUrgentMaintenance { get; set; }
}

public class HardwarePageViewModel
{
    public int TotalAssetsCount { get; set; } = 124;
    public string TotalAssetsSubtext { get; set; } = "8 โรงหนังครอบคลุมทั้งหมด";
    public string OnlineStandbyRate { get; set; } = "96.2%";
    public string OnlineStandbySubtext { get; set; } = "มีออฟไลน์ชั่วคราว 4 ชิ้น";
    public int DueForMaintenanceCount { get; set; } = 2;
    public string DueForMaintenanceSubtext { get; set; } = "กำหนดตรวจสอบรอบเช้านี้";

    public List<HardwareAssetDto> Assets { get; set; } = new();
}

public class ShiftHandoverLogDto
{
    public int Id { get; set; }
    public string Title { get; set; } = ""; // ส่งมอบเวรเช้า -> บ่าย
    public string SenderName { get; set; } = ""; // วรพล ดีใจ
    public string ReceiverName { get; set; } = ""; // กิตติพงษ์ มีสุข
    public string TimeText { get; set; } = ""; // วันนี้ • 16:30 น.
    public string Note { get; set; } = "";
    public string StatusBadge { get; set; } = ""; // มีงานด่วนค้างคา
    public string DotColor { get; set; } = "green"; // yellow, green
}

public class ShiftLogPageViewModel
{
    public string CurrentShiftTitle { get; set; } = "เวรปฏิบัติการปัจจุบัน: เวรบ่าย (ช่างกะสอง)";
    public string CurrentShiftSubtext { get; set; } = "รับผิดชอบหลักโดย กิตติพงษ์ • สมาชิกร่วมทีมออนไซต์ 3 คน • กำหนดเวลาส่งมอบรอบถัดไป 23:30 น.";
    public string CurrentShiftDuration { get; set; } = "เข้ากะแล้ว 4 ชม.";

    public List<ShiftHandoverLogDto> Logs { get; set; } = new();
}

public class TechChatRoomDto
{
    public string Id { get; set; } = ""; // tech-ops, tech-oncall, manager, facility
    public string Name { get; set; } = ""; // กลุ่มช่างปฏิบัติการหลัก
    public string LastMessageText { get; set; } = "";
    public string LastMessageTime { get; set; } = "";
    public int UnreadCount { get; set; }
    public bool IsActive { get; set; }
    public string AvatarColor { get; set; } = "green";
    public string OnlineStatusText { get; set; } = "ออนไลน์ 4 คนในสาขา";
}

public class TechChatMessageDto
{
    public int Id { get; set; }
    public string ChannelId { get; set; } = "tech-ops";
    public string SenderName { get; set; } = "";
    public string TimeText { get; set; } = "";
    public string MessageText { get; set; } = "";
    public bool IsMine { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class TechChatPageViewModel
{
    public string ActiveRoomId { get; set; } = "tech-ops";
    public TechChatRoomDto ActiveRoom { get; set; } = new();
    public List<TechChatRoomDto> Rooms { get; set; } = new();
    public List<TechChatMessageDto> Messages { get; set; } = new();
    public int TotalUnreadCount { get; set; }
}

public class CreateIncidentDto
{
    public string Title { get; set; } = "";
    public string CinemaHall { get; set; } = "โรง 2";
    public string Assignee { get; set; } = "อนุชา";
    public string Description { get; set; } = "";
    public string Urgency { get; set; } = "เร่งด่วน";
    public string Detector { get; set; } = "เจ้าหน้าที่ฝ่ายเทคนิค";
}

public class UpdateIncidentResultDto
{
    public string Code { get; set; } = "";
    public string NewStatus { get; set; } = "";
    public string ActivityTitle { get; set; } = "";
    public string ActivityDescription { get; set; } = "";
}

public class CreateMaintenanceDto
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "ตามแผน";
    public string Assignee { get; set; } = "อนุชา • ช่างเทคนิค";
    public string ScheduleTime { get; set; } = "วันนี้ • 14:00 น.";
    public string CinemaHall { get; set; } = "โรง 1";
}

public class CreateHardwareDto
{
    public string NameAndModel { get; set; } = "";
    public string Category { get; set; } = "ระบบฉายภาพ";
    public string CinemaHall { get; set; } = "โรง 1";
    public string Status { get; set; } = "ปกติ";
}

public class CreateShiftLogDto
{
    public string ShiftTitle { get; set; } = "ส่งมอบเวรบ่าย -> ดึก";
    public string SenderName { get; set; } = "กิตติพงษ์ มีสุข";
    public string ReceiverName { get; set; } = "อนุชา ทนทาน";
    public string Note { get; set; } = "";
    public bool HasUrgentPending { get; set; }
}
