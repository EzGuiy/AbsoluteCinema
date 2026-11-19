using System;
using System.Collections.Generic;

namespace Kawin_67091861_002.Models;

public partial class Role
{
    public int RoleId { get; set; }

    public string RoleName { get; set; } = null!;

    public virtual ICollection<Chat> Chats { get; set; } = new List<Chat>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
