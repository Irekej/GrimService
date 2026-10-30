using System.Collections.Generic;
namespace GrimService.Features.Bootstrap
{
    public sealed record BootstrapOption(string Key, string Value);
    public sealed record BootstrapServer(string Name, string Host, string Port, string Url, IReadOnlyList<BootstrapOption> Options);
    public sealed record BootstrapProfile(string Id, string Path, IReadOnlyList<BootstrapServer> Servers);
    public static class BootstrapDefaults
    {
        private static readonly BootstrapServer Section0 = new("gt", null, null, null, new BootstrapOption[] {
            new("online.language.set", "0"),
            new("online.serverlist2.enable", "0"),
            new("online.eula.enable", "0"),
            new("online.ranking.license.enable", "1"),
            new("online.quickmatch.enable", "1"),
            new("patch.enable", "0"),
            new("patch.seq469128999", "1"),
            new("lobby_id", "12"),
            new("online.course.max", "8"),
            new("online.photo.max", "8"),
            new("disqualify_report", "1"),
            new("online.gtnews.enable", "1"),
        });
        private static readonly BootstrapServer Section1 = new("game", null, null, null, new BootstrapOption[] {
            new("room.prize_base", "150"),
            new("room.prize_mode_pr", "100"),
            new("room.prize_mode_pqr", "100"),
            new("room.prize_mode_pt", "100"),
            new("room.prize_mode_pd", "100"),
            new("room.prize_mode_pe", "100"),
            new("room.prize_mode_pqe", "100"),
            new("room.prize_order_table", "100,70,60,55,53,50,48,45,43,40,38,35,33,30,28,25"),
            new("room.prize_entries_num_table", "100,100,100,100,100,100,105,110,115,120,125,130,135,140,145,150"),
            new("qmatch.prize_entries_num_table", "100,100,100,100,100,100,105,110,115,120,125,130,135,140,145,150"),
            new("qmatch.prize_base", "100"),
            new("qmatch.crowded_threshold", "1,20,40,60,100"),
        });
        private static readonly BootstrapServer Section2 = new("network", null, null, null, new BootstrapOption[] {
            new("qmatch.wpCountDown", "180,180"),
            new("qmatch.wpCountDownExtension", "5"),
            new("qmatch.joinRetryableCount", "15"),
            new("qmatch.joinRetryInterval", "2000"),
            new("lobby.joinRetryableCount", "-1"),
            new("lobby.joinRetryInterval", "-1"),
            new("community.presence", "180"),
        });
        private static readonly BootstrapServer Section3 = new("grim_http", "54.37.232.59", "80", "http://54.37.232.59", new BootstrapOption[] {
            new("online.log.enable", "1"),
            new("updater.delay", "10"),
            new("online.extendsession.interval", "2"),
            new("maintenance.next", "2099-03-28T21:00:00Z"),
            new("maintenance.next.display", "2099-03-28T20:00:00Z"),
            new("maintenance", "0"),
            new("maintenance.message", "Custom maintenance message"),
        });
        private static readonly BootstrapServer Section4 = new("grim2_http", null, null, null, new BootstrapOption[] {
            new("sendBufferSize", "8191"),
            new("recvBufferSize", "61439"),
            new("connectionTimeout", "13000000000000000"),
            new("requestTimeout", "1300000000000000"),
            new("responseTimeout", "1300000000000000"),
            new("multiplicity", "-1"),
        });
        private static readonly BootstrapServer Section5 = new("gtacademy", null, null, null, new BootstrapOption[] {
            new("scee.eligible_region", "fr,it,gb,ru,es,pt,nl,be,lu,pl,cz,se,de,in,au,ae,qa,om,bh,sa,lb,kw"),
        });
        private static readonly BootstrapServer Section6 = new("gt", null, null, null, new BootstrapOption[] {
            new("online.language.set", "0"),
            new("online.serverlist2.enable", "0"),
            new("online.eula.enable", "0"),
            new("online.ranking.license.enable", "1"),
            new("online.quickmatch.enable", "1"),
            new("patch.enable", "0"),
            new("patch.seq469128999", "1"),
            new("lobby_id", "3"),
            new("online.photo.max", "8"),
            new("online.club.max", "8"),
            new("online.course.max", "8"),
            new("disqualify_report", "1"),
            new("online.gtnews.enable", "1"),
        });
        private static readonly BootstrapServer Section7 = new("grim_http", "54.37.232.59", "80", "http://vps-0213c442.vps.ovh.net////", new BootstrapOption[] {
            new("online.log.enable", "1"),
            new("updater.delay", "10"),
            new("online.extendsession.interval", "2"),
            new("maintenance.next", "2099-03-28T21:00:00Z"),
            new("maintenance.next.display", "2099-03-28T20:00:00Z"),
            new("maintenance", "0"),
            new("maintenance.message", "Server Offline!"),
        });
        private static readonly BootstrapServer Section8 = new("grim2_http", null, null, null, new BootstrapOption[] {
            new("sendBufferSize", "8191"),
            new("recvBufferSize", "61439"),
            new("connectionTimeout", "130000"),
            new("requestTimeout", "1300000"),
            new("responseTimeout", "1300000"),
            new("multiplicity", "-1"),
        });
        private static readonly BootstrapServer Section9 = new("grim_http", "54.37.232.59", "80", "test.5.61.32.177", new BootstrapOption[] {
            new("online.log.enable", "1"),
            new("updater.delay", "10"),
            new("online.extendsession.interval", "2"),
            new("maintenance.next", "2099-03-28T21:00:00Z"),
            new("maintenance.next.display", "2099-03-28T20:00:00Z"),
            new("maintenance", "0"),
            new("maintenance.message", "Server Offline!"),
        });
        private static readonly BootstrapServer Section10 = new("grim_http", "192.168.1.110", "80", "http://192.168.1.110", new BootstrapOption[] {
            new("online.log.enable", "1"),
            new("updater.delay", "10"),
            new("online.extendsession.interval", "2"),
            new("maintenance.next", "2099-03-28T21:00:00Z"),
            new("maintenance.next.display", "2099-03-28T20:00:00Z"),
            new("maintenance", "0"),
            new("maintenance.message", "Server Offline!"),
        });
        private static readonly BootstrapServer Section11 = new("gt", null, null, null, new BootstrapOption[] {
            new("online.language.set", "0"),
            new("online.serverlist2.enable", "0"),
            new("online.eula.enable", "0"),
            new("online.ranking.license.enable", "1"),
            new("online.quickmatch.enable", "1"),
            new("patch.enable", "0"),
            new("patch.seq469128999", "1"),
            new("lobby_id", "12"),
            new("online.photo.max", "8"),
            new("disqualify_report", "1"),
            new("online.gtnews.enable", "1"),
        });
        private static readonly BootstrapServer Section12 = new("grim_http", "5.61.32.177", "80", "http://5.61.32.177", new BootstrapOption[] {
            new("online.log.enable", "1"),
            new("updater.delay", "10"),
            new("online.extendsession.interval", "2"),
            new("maintenance.next", "2099-03-28T21:00:00Z"),
            new("maintenance.next.display", "2099-03-28T20:00:00Z"),
            new("maintenance", "0"),
            new("maintenance.message", "Server Offline!"),
        });
        private static readonly BootstrapServer Section13 = new("gt5", null, null, null, new BootstrapOption[] {
            new("online.language.set", "0"),
            new("online.serverlist2.enable", "0"),
            new("online.eula.enable", "1"),
            new("online.ranking.license.enable", "1"),
            new("online.quickmatch.enable", "1"),
            new("present.car.price_limit", "20000000"),
            new("online.credit.level1", "10000"),
            new("online.credit.level2", "50000"),
            new("online.credit.level3", "200000"),
            new("online.photo.max", "8"),
            new("online.course.max", "1"),
            new("online.pace.enable", "1"),
        });
        private static readonly BootstrapServer Section14 = new("gt5p", null, null, null, new BootstrapOption[] {
            new("version", "1"),
            new("online.museum.enable", "1"),
            new("online.gtnews.enable", "1"),
            new("online.gttv.enable", "1"),
            new("online.timetrial.enable", "1"),
            new("online.timetrial.areaid", "0"),
            new("online.eula", "1"),
            new("online.eula.version", "1"),
        });
        private static readonly BootstrapServer Section15 = new("game", null, null, null, new BootstrapOption[] {
            new("room.prize_base", "1500"),
            new("room.user_bgm", "1"),
            new("room.pause_rendering", "0"),
            new("room.prize_level_table", "100,70,60,55,53,50,48,45,43,40,38,35,33,30,28,25"),
            new("room.prize_order_table", "100,70,60,55,53,50,48,45,43,40,38,35,33,30,28,25"),
            new("room.exp_level_table", "100,70,60,55,53,50,48,45,43,40,38,35,33,30,28,25"),
            new("room.exp_order_table", "100,70,60,55,53,50,48,45,43,40,38,35,33,30,28,25"),
            new("room.exp_base", "0"),
            new("room.remote_racer_num_base", "16"),
        });
        private static readonly BootstrapServer Section16 = new("grim_http", null, null, null, new BootstrapOption[] {
            new("online.log.enable", "1"),
            new("updater.delay", "1000"),
            new("updater.museumrate", "1000"),
            new("online.extendsession.interval", "600"),
            new("maintenance.next", "2021-03-28T21:00:00Z"),
            new("maintenance.next.display", "2021-03-28T20:00:00Z"),
            new("maintenance", "0"),
            new("maintenance.message", "Custom maintenance messages"),
        });
        private static readonly BootstrapServer Section17 = new("grim2_http", null, null, null, new BootstrapOption[] {
            new("sendBufferSize", "8191"),
            new("recvBufferSize", "61439"),
            new("connectionTimeout", "30000"),
            new("requestTimeout", "120000"),
            new("responseTimeout", "120000"),
            new("multiplicity", "-1"),
        });
        private static readonly BootstrapServer Section18 = new("gtacademy", null, null, null, new BootstrapOption[] {
            new("scee.eligible_region", "fr,it,gb,ru,es,pt,nl,be,lu,pl,cz,se,de,in,au,ae,qa,om,bh,sa,lb,kw"),
            new("make.believe", "0"),
        });
        private static readonly BootstrapServer Section19 = new("pace", null, null, null, new BootstrapOption[] {
            new("Support.PaceDebugPopup.enable", "1"),
            new("suburi1", "1"),
            new("suburi2", "2"),
            new("suburi3", "3"),
            new("suburi4", "4"),
        });
        public static readonly IReadOnlyList<BootstrapProfile> Profiles = new BootstrapProfile[] {
            new("default", "serverlist.xml", new BootstrapServer[] { Section0, Section1, Section2, Section3, Section4, Section5 }),
            new("dev", "dev/serverlist.xml", new BootstrapServer[] { Section6, Section1, Section2, Section7, Section8, Section5 }),
            new("dev-ofw", "dev/serverlistofw.xml", new BootstrapServer[] { Section6, Section1, Section2, Section9, Section8, Section5 }),
            new("dev-local", "dev/serverlistlocal.xml", new BootstrapServer[] { Section6, Section1, Section2, Section10, Section8, Section5 }),
            new("rel", "rel/serverlist.xml", new BootstrapServer[] { Section11, Section1, Section2, Section12, Section8, Section5 }),
            new("gt5-template", "serverlist_gt5.xml", new BootstrapServer[] { Section13, Section14, Section15, Section16, Section17, Section18, Section19 }),
        };
    }
}
