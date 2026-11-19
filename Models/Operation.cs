using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.Models;

public partial class Operation
{
    public int OperationId { get; set; }

    public int CinemaId { get; set; }

    public int ReportedBy { get; set; }

    public string TaskType { get; set; } = null!;

    public string? Description { get; set; }

    public string? Status { get; set; }

    public virtual Cinema Cinema { get; set; } = null!;

    public virtual User ReportedByNavigation { get; set; } = null!;
}
