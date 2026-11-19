using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.Models;

public partial class Chat
{
    public int ChatId { get; set; }

    public int SenderId { get; set; }

    public int? TargetRole { get; set; }

    public int? ReceiverId { get; set; }

    public string Message { get; set; } = null!;

    public DateTime? SentAt { get; set; }

    public virtual User? Receiver { get; set; }

    public virtual User Sender { get; set; } = null!;

    public virtual Role? TargetRoleNavigation { get; set; }
}
