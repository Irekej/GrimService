using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using GrimService.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrimService.Services.Rpcn
{

    public sealed class RpcnRelayRegistrationService : BackgroundService
    {
        private readonly RpcnRelayOptions cfg;
        private readonly ILogger<RpcnRelayRegistrationService> log;
        public RpcnRelayRegistrationService(IOptions<RpcnRelayOptions> options, ILogger<RpcnRelayRegistrationService> logger)
        { cfg=options.Value;log=logger; }
        protected override async Task ExecuteAsync(CancellationToken stop)
        {
            if (!cfg.Enabled || !cfg.EnableRegistrationRelay) return;
            using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(stop);
            try
            {
                var ip=IPAddress.Parse(cfg.ListenAddress);
                if(!IPAddress.IsLoopback(ip))throw new InvalidOperationException("UDP registration relay is loopback-only");
                using var local=new UdpClient(new IPEndPoint(ip,cfg.RegistrationListenPort));
                using var upstream=new UdpClient(AddressFamily.InterNetwork);
                var addresses=await Dns.GetHostAddressesAsync(cfg.UpstreamHost,stop);
                upstream.Connect(addresses.First(x=>x.AddressFamily==AddressFamily.InterNetwork),cfg.RegistrationUpstreamPort);
                IPEndPoint client=null;long sent=0,received=0;
                log.LogWarning("RPCN diagnostic UDP registration proxy enabled on {Address}:{Port}; advertised UDP endpoint changes; NOT a P2P gameplay proxy",ip,cfg.RegistrationListenPort);
                var outgoing=Out();var incoming=In();await Task.WhenAny(outgoing,incoming);
                lifetime.Cancel();local.Close();upstream.Close();
                try{await Task.WhenAll(outgoing,incoming);}catch(OperationCanceledException)when(lifetime.IsCancellationRequested){}
                async Task Out()
                {
                    while(!lifetime.IsCancellationRequested)
                    {
                        var d=await local.ReceiveAsync(lifetime.Token);
                        if(!IPAddress.IsLoopback(d.RemoteEndPoint.Address)||d.RemoteEndPoint.Port!=3658||d.Buffer.Length!=13||d.Buffer[0]!=1)continue;
                        if(client!=null&&!client.Equals(d.RemoteEndPoint))continue;
                        client=d.RemoteEndPoint;
                        await upstream.SendAsync(d.Buffer,lifetime.Token);
                        if(++sent==1)log.LogInformation("RPCN UDP registration request forwarded (13 bytes; identity omitted)");
                    }
                }
                async Task In()
                {
                    while(!lifetime.IsCancellationRequested)
                    {
                        var d=await upstream.ReceiveAsync(lifetime.Token);
                        if(client==null||d.Buffer.Length!=9||d.Buffer[0]!=0||d.Buffer[1]!=0||d.Buffer[2]!=0)continue;
                        await local.SendAsync(d.Buffer,client,lifetime.Token);
                        if(++received==1)log.LogInformation("RPCN UDP registration reply forwarded (9 bytes); diagnostic proxy is not transparent for peers");
                    }
                }
            }
            catch(OperationCanceledException)when(stop.IsCancellationRequested){}
            catch(Exception ex){log.LogError("RPCN registration relay stopped: {ErrorType}",ex.GetType().Name);}
        }
    }
}
