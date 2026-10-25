namespace GrimService.Models
{
    public sealed class TutorialViewModel
    {
        public string Game { get; set; }
        public string Platform { get; set; }
        public string Installation { get; set; }
        public string Region { get; set; }
        public bool PackageAvailable { get; set; }
        public bool ManualAvailable { get; set; }
        public bool CrossplayAvailable { get; set; }
        public System.Collections.Generic.IReadOnlyList<string> Regions => Platform == "PS3-CROSSPLAY"
            ? Services.TutorialAssets.CrossplayRegions(Game) : Services.TutorialAssets.Regions(Game);
        public string GameVersion => Game == "GT6" ? "1.22" : "2.11";
        public string CrossplayExecutable => Game == "GT6" ? "EMAIN.SELF" : "EBOOT.BIN";
        public string CrossplayModule => Region switch
        {
            "BCAS20108" => "grim_rpcn_bcas",
            "BCJS30001" => "grim_rpcn_bcjs",
            "BCUS98114" => "grim_rpcn_bcus",
            "BCES01893" => "grim_rpcn_gt6",
            "BCAS25018" => "grim_rpcn_gt6_bcas25018",
            "BCES01977" => "grim_rpcn_gt6_bces01977",
            "BCJS37016" => "grim_rpcn_gt6_bcjs37016",
            "BCUS98296" => "grim_rpcn_gt6_bcus98296",
            "BCUS99247" => "grim_rpcn_gt6_bcus99247",
            _ => "grim_rpcn"
        };
    }
}
