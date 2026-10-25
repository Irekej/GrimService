using System;

namespace GrimService.Features.Resources;

public static class ClientResourceRoutes
{
    public static string GreetingList(string server, string region) => $"greeting/{server}/{region}/greeting_list.xml";
    public static string GreetingImage(string server, string region, string image) => $"greeting/{server}/{region}/{image}.img";
    public const string PatchInfo = "patch/patchinfo.xml";
    public static string TelevisionImage(string type, int id) => $"data/tv/common/tv2_l_{type}_{id}.img";

    public static string Television(string server, string region, string fileRegion, string selector)
    {
        if (selector == null) return null;
        string tail;
        if (selector == "root") tail = "root";
        else if (selector.StartsWith("l_", StringComparison.Ordinal) && int.TryParse(selector.AsSpan(2), out int list)) tail = "l_" + list;
        else if (selector.StartsWith("s_", StringComparison.Ordinal) && int.TryParse(selector.AsSpan(2), out int set)) tail = "s_" + set;
        else if (int.TryParse(selector, out int item)) tail = item.ToString();
        else return null;
        return $"data2/tv/{server}/{region}/tv2_{fileRegion}_{tail}.xml";
    }
}
