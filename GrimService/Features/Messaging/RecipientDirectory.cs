using System.Threading.Tasks;
using GrimService.Database.Controllers;

namespace GrimService.Features.Messaging
{
    public interface IRecipientDirectory
    {
        Task<int?> FindAsync(string username);
    }

    public sealed class RecipientDirectory : IRecipientDirectory
    {
        private readonly UserDBManager _users;
        public RecipientDirectory(UserDBManager users) => _users = users;
        public async Task<int?> FindAsync(string username) => (await _users.GetByPSNUserIdAsync(username))?.Id;
    }
}
