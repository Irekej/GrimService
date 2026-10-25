using System;
using System.Collections.Generic;
using GrimService.Database.Tables;

namespace GrimService.Models.Admin
{
    public class AdminDashboardViewModel
    {
        public int TotalPlayers { get; set; }
        public int BannedPlayers { get; set; }
        public int ConnectedPlayers { get; set; }
        public IReadOnlyList<UserDTO> Players { get; set; } = Array.Empty<UserDTO>();
        public string Query { get; set; }
        public string Game { get; set; }
        public IReadOnlyList<AdminUserDTO> Administrators { get; set; } = Array.Empty<AdminUserDTO>();
    }

    public class AdminPlayerViewModel
    {
        public UserDTO Player { get; set; }
        public IReadOnlyList<CourseDTO> Courses { get; set; } = Array.Empty<CourseDTO>();
        public IReadOnlyList<PhotoDTO> Photos { get; set; } = Array.Empty<PhotoDTO>();
        public IReadOnlyList<ReplayDTO> Replays { get; set; } = Array.Empty<ReplayDTO>();
        public bool IsConnected { get; set; }
        public string BanReason { get; set; }
    }
}
