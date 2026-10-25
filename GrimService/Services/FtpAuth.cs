using System.Threading.Tasks;
using FubarDev.FtpServer.AccountManagement;

namespace GrimService.Services
{

    public class SimpleFtpUser : IFtpUser
    {
        public SimpleFtpUser(string name, string rootPath)
        {
            Name = name;
            RootPath = rootPath;
        }

        public string Name { get; }


        public bool IsInGroup(string groupName) => false;


        public string RootPath { get; }
    }


    public class SimpleMembershipProvider : IMembershipProvider
    {
        private readonly string _username;
        private readonly string _password;
        private readonly string _rootPath;

        public SimpleMembershipProvider(string username, string password, string rootPath)
        {
            _username = username;
            _password = password;
            _rootPath = rootPath;
        }


        public Task<MemberValidationResult> ValidateUserAsync(string username, string password)
        {
            if (!string.IsNullOrWhiteSpace(_username) && !string.IsNullOrEmpty(_password) &&
                username == _username && password == _password)
            {
                var user = new SimpleFtpUser(username, _rootPath);

                var result = new MemberValidationResult(MemberValidationStatus.AuthenticatedUser, user);
                return Task.FromResult(result);
            }


            var invalid = new MemberValidationResult(MemberValidationStatus.InvalidLogin);
            return Task.FromResult(invalid);
        }
    }
}
