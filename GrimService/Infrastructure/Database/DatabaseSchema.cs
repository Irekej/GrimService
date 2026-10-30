using System;
using GrimService.Database.Controllers;
using GrimService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrimService.Infrastructure.Database
{
    internal static class DatabaseSchema
    {
        public static void Initialize(IServiceProvider services, IConfiguration configuration)
        {
            Run<UserDBManager>(services, manager => manager.CreateTable());
            Run<AdminUserDBManager>(services, manager => manager.CreateTable());
            Run<GrimService.Features.Bootstrap.BootstrapSettingsService>(services, manager => manager.CreateTable());
            Run<NewsService>(services, manager => manager.CreateTable());
            Run<ReportService>(services, manager => manager.CreateTable());
            Run<AdminUserDBManager>(services, manager => manager.SynchronizeBootstrapUserAsync(
                configuration["Admin:Username"], configuration["Admin:Password"], "bootstrap").GetAwaiter().GetResult(),
                "SynchronizeBootstrapUser");
            Run<FriendDBManager>(services, manager => manager.CreateTable());
            Run<UserSpecialDBManager>(services, manager => manager.CreateTable());
            Run<CourseDBManager>(services, manager => manager.CreateTable());
            Run<CourseGT5DBManager>(services, manager => manager.CreateTable());
            Run<BbsBoardDBManager>(services, manager => manager.CreateTable());
            Run<ActionLogDBManager>(services, manager => manager.CreateTable());
            Run<PhotoDBManager>(services, manager => manager.CreateTable());
            Run<RankingsDBManager>(services, manager => manager.CreateTable());
            Run<BSpecCarDBManager>(services, manager => manager.CreateTable());
            Run<BSpecDriverDBManager>(services, manager => manager.CreateTable());
            Run<BSpecRaceDBManager>(services, manager => manager.CreateTable());
            Run<GTMailDBManager>(services, manager => manager.CreateTable());
            Run<GT6MailDBManager>(services, manager => manager.CreateTable());
            Run<ItemBoxDBManager>(services, manager => manager.CreateTable());
            Run<ReplayDBManager>(services, manager => manager.CreateTable());
            Run<QuickMatchDBManager>(services, manager => manager.CreateTable());
            Run<PrivateRoomsDBManager>(services, manager => manager.CreateTable());
            Run<PresenceDBManager>(services, manager => manager.CreateTable());
            Run<ClubsDBManager>(services, manager => manager.CreateTable());
            Run<ClubContentDBManager>(services, manager => manager.CreateTable());
            Run<GT6SocialDBManager>(services, manager => manager.CreateTable());
        }

        private static void Run<T>(IServiceProvider services, Action<T> action, string operation = "CreateTable") where T : notnull
        {
            try
            {
                action(services.GetRequiredService<T>());
            }
            catch (Exception exception)
            {
                throw new DatabaseSchemaException(typeof(T).Name + "." + operation, exception);
            }
        }
    }

    internal sealed class DatabaseSchemaException : InvalidOperationException
    {
        public DatabaseSchemaException(string step, Exception cause)
            : base($"Database initialization step '{step}' failed.", cause) { }
    }
}
