using System;
using System.Collections.Generic;

namespace GrimService.Features.Diagnostics;

public readonly record struct ClientLogMessage(bool IsCrash, string Text);

public static class ClientLogDecoder
{
    public static bool TryDecode(string header, IReadOnlyDictionary<string, string> codes, out ClientLogMessage message)
    {
        message = default;
        if (header == null) return false;
        if (header.StartsWith("ADHOC", StringComparison.Ordinal))
        {
            message = new(true, header);
            return true;
        }
        var fields = header.Split(':');
        ExpandInPlace(fields, codes);
        message = new(false, string.Join(':', fields));
        return true;
    }

    public static string[] ExpandInPlace(string[] fields, IReadOnlyDictionary<string, string> codes)
    {
        if (fields == null) return null;
        for (int index = 0; index < fields.Length; index++)
        {
            string field = fields[index];
            if (field == null || field.Length <= 2) continue;
            if (codes.TryGetValue(field[..2], out string label)) fields[index] = label + field[2..];
        }
        return fields;
    }
}
