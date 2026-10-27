using FubarDev.FtpServer;
using FubarDev.FtpServer.AccountManagement;
using FubarDev.FtpServer.FileSystem;
using FubarDev.FtpServer.FileSystem.DotNet;
using GrimService.Config;
using GrimService.Features.Clubs;
using GrimService.Features.ClubPhotos;
using GrimService.Features.EventCatalog;
using GrimService.Features.Forum;
using GrimService.Features.Gifts;
using GrimService.Features.Messaging;
using GrimService.Features.Moderation;
using GrimService.Features.Rankings;
using GrimService.Features.Replays;
using GrimService.Features.Locale;
using GrimService.Features.PlayerRecords;
using GrimService.Infrastructure.Sessions;
using GrimService.Infrastructure.Database;
using GrimService.Infrastructure.Security;
using GrimService.Infrastructure.Http;
using GrimService.Infrastructure.Protocol;
using GrimService.Controllers;
using GrimService.Database.Controllers;
using GrimService.Filters;
using GrimService.Models;
using GrimService.Services;
using GrimService.Utils;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;


namespace GrimService.Startup
{
    public class Startup
    {

        public IConfiguration Configuration { get; }
        public ILogger<Startup> Logger { get; private set; }


        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            Console.WriteLine("Init: Configuring services");


            services.AddControllersWithViews();
            services.AddSingleton<GrimService.Services.SprxUpdateCatalog>();
            services.AddHttpContextAccessor();
            services.AddSingleton<GrimService.Services.WebTranslations>();
            services.AddScoped<GrimService.Services.WebText>();
            services.AddEventCatalogFeatures();
            services.AddClientLocale();
            services.AddPlayerRecords();
            services.AddPlayerLibraries();
            services.AddGameRequestSessions();
            services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

            AddJWTAuthentication(services);
            services.AddAdminAuthentication();

            services.AddGameXmlProtocol();

            var ftpRootPath = Path.Combine(AppContext.BaseDirectory, "Resources", "event", "dev");
            if (!Directory.Exists(ftpRootPath))
            {
                Directory.CreateDirectory(ftpRootPath);
            }


            services.Configure<DotNetFileSystemOptions>(opt => opt.RootPath = ftpRootPath);
            services.AddSingleton<IFileSystemClassFactory, DotNetFileSystemProvider>();



            services.AddSingleton<IMembershipProvider>(sp => new SimpleMembershipProvider(Configuration["Ftp:Username"], Configuration["Ftp:Password"], ftpRootPath));


            services.AddFtpServer(builder =>
            {
                builder
                    .UseDotNetFileSystem();

            });


            services.Configure<FtpServerOptions>(opt =>
            {
                opt.ServerAddress = "0.0.0.0";
                opt.Port = 2225;
            });

            services.Configure<SimplePasvOptions>(opt =>
            {
                opt.PasvMinPort = 50000;
                opt.PasvMaxPort = 50100;
            });


            services.AddHostedService<FtpServerHostedService>();




            services.Configure<GameServerOptions>(Configuration.GetSection(GameServerOptions.GameServer));
            services.Configure<RpcnBridgeOptions>(Configuration.GetSection(RpcnBridgeOptions.Section));
            services.Configure<RpcnRoomsOptions>(Configuration.GetSection("RpcnRooms"));
            services.AddSingleton<GrimService.Services.Rpcn.RpcnRoomsService>();
            services.AddHostedService(sp => sp.GetRequiredService<GrimService.Services.Rpcn.RpcnRoomsService>());
            services.Configure<RpcnRelayOptions>(Configuration.GetSection(RpcnRelayOptions.Section));
            services.AddHostedService<GrimService.Services.Rpcn.RpcnRelayService>();
            services.AddHostedService<GrimService.Services.Rpcn.RpcnRelayRegistrationService>();
            services.AddSingleton<GrimService.Services.Rpcn.RpcnBridge>();
            services.AddSingleton<GrimService.Services.Rpcn.RpcnCustomTicket>();
            services.AddSingleton<GrimService.Services.Rpcn.RpcnTicketVerifier>();
            services.AddRateLimiter(options => {
                options.RejectionStatusCode = 429;
                options.AddPolicy("rpcn-bridge", context => System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions {
                        PermitLimit = 12, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
                    }));
            });
            services.AddSingleton<PlayerManager>();
            services.AddTransient<IDbConnection>((sp) => new NpgsqlConnection(Configuration["Database:ConnectionString"]));

            services.AddTransient<UserDBManager>();
            services.AddTransient<AdminUserDBManager>();
            services.AddSingleton<SeasonalEventService>();
            services.AddTransient<NewsService>();
            services.AddModerationFeatures();
            services.AddClubFeatures();
            services.AddTransient<FriendDBManager>();
            services.AddTransient<UserSpecialDBManager>();
            services.AddTransient<BbsBoardDBManager>();
            services.AddTransient<CourseDBManager>();
            services.AddTransient<CourseGT5DBManager>();
            services.AddTransient<ActionLogDBManager>();
            services.AddTransient<PhotoDBManager>();
            services.AddTransient<RankingsDBManager>();
            services.AddRankingFeatures();
            services.AddTransient<BSpecCarDBManager>();
            services.AddTransient<BSpecDriverDBManager>();
            services.AddTransient<BSpecRaceDBManager>();
            services.AddTransient<GTMailDBManager>();
            services.AddTransient<GT6MailDBManager>();
            services.AddMessagingFeatures();
            services.AddGiftFeatures();
            services.AddTransient<ItemBoxDBManager>();
            services.AddTransient<ReplayDBManager>();
            services.AddReplayFeatures();
            services.AddSingleton<WeeklyDealershipService>();
            services.AddSingleton<QuickMatchService>();
            services.AddTransient<QuickMatchDBManager>();
            services.AddTransient<PrivateRoomsDBManager>();
            services.AddTransient<PresenceDBManager>();
            services.AddTransient<ClubsDBManager>();
            services.AddTransient<ClubContentDBManager>();
            services.AddForumFeatures();
            services.AddClubPhotoFeatures();
            services.AddTransient<GT6SocialDBManager>();

            Console.WriteLine("Init: All services load success");
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILogger<Startup> logger)
        {
            app.UseClientAssets();
            Logger = logger;
            Console.WriteLine("Init: Configuring database");

            try
            {
                InitDatabase(app.ApplicationServices);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Init: Unable to init database. Make sure that the SQL service is running, and a connection to it is possible. {e.Message}");
                throw;
            }

            app.UseGameEndpoints(logger);
            Console.WriteLine("Init: Done starting server.");

        }

        public void InitDatabase(IServiceProvider services)
        {
            new DatabaseInitializer(services, Configuration).Initialize();
        }

        public void AddJWTAuthentication(IServiceCollection services)
        {
            services.AddGameAuthentication(Configuration);
        }

    }
}
