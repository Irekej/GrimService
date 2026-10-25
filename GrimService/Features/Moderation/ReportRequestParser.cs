using System;
using System.Collections.Generic;
using System.Linq;
using GrimService.Models.Xml;

namespace GrimService.Features.Moderation
{
    public static class ReportRequestParser
    {
        public static SortedDictionary<string,string> Parse(GrimRequest request)
        {
            if (request == null) throw new ArgumentException("Missing report request.");
            var modern = request.Command == "report.send";
            if (!modern && request.Command != "report.sendreport") throw new ArgumentException("Unknown report command.");
            var fields = new SortedDictionary<string,string>(StringComparer.Ordinal);
            var allowed = new[] { "region", "language", "target_user_id", "user_id", "type", "what", "key", "value1", "comment" };
            foreach (var p in request.Params?.ParamList ?? throw new ArgumentException("Missing report parameters."))
            {
                if (p == null || !allowed.Contains(p.Key)) continue;
                if (fields.ContainsKey(p.Key) || (p.Text?.Length ?? 0) > 16000) throw new ArgumentException("Invalid report parameter.");
                fields.Add(p.Key, p.Text ?? "");
            }
            foreach (var key in modern ? new[] { "target_user_id", "type", "what", "key" } : new[] { "user_id" })
                if (!fields.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v) || v.Length > 512)
                    throw new ArgumentException("Missing or invalid report field: " + key);
            if (!modern && !fields.ContainsKey("comment")) throw new ArgumentException("Missing comment.");
            fields["command"] = request.Command;
            return fields;
        }
    }
}
