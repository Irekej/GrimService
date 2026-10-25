using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FubarDev.FtpServer;

namespace GrimService.Services
{
    public class FtpServerHostedService : IHostedService
    {
        private readonly IFtpServerHost _ftpServerHost;
        private readonly ILogger<FtpServerHostedService> _logger;

        public FtpServerHostedService(IFtpServerHost ftpServerHost, ILogger<FtpServerHostedService> logger)
        {
            _ftpServerHost = ftpServerHost;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting FTP server...");
            await _ftpServerHost.StartAsync(cancellationToken);
            _logger.LogInformation("FTP server started.");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping FTP server...");
            await _ftpServerHost.StopAsync(cancellationToken);
            _logger.LogInformation("FTP server stopped.");
        }
    }
}
