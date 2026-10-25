using System;

namespace GrimService.Database.Tables
{
    public class AdminUserDTO
    {
        public long Id { get; set; }
        public bool IsAdmin { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; }
    }
}
