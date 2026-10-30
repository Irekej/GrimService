using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace GrimService.Services;

// Files stay loose and external to the single-file server executable. Operators
// upload GAME_TITLE_000REV.sprx, preferably as .upload then rename on completion.
public sealed class SprxUpdateCatalog
{
    public const int MaximumFileSize = 2 * 1024 * 1024;
    private static readonly Dictionary<string, string[]> Regions = new(StringComparer.Ordinal)
    {
        ["GT5"] = new[] { "BCES00569", "BCJS30001", "BCUS98114", "BCAS20108" },
        ["GT6"] = new[] { "BCES01893", "BCES01977", "BCAS25018", "BCJS37016", "BCUS98296", "BCUS99247" }
    };
    private static readonly Regex FilePattern = new(@"^(GT[56])_([A-Z]{4}[0-9]{5})_([0-9]{6})\.sprx$", RegexOptions.CultureInvariant);
    private readonly string folder;
    private readonly string keyPath;
    private readonly string publicKeyPath;
    public SprxUpdateCatalog(IWebHostEnvironment env, IConfiguration config)
    {
        folder = Path.GetFullPath(config["SprxUpdates:Directory"] ?? Path.Combine(env.ContentRootPath, "Resources", "tutorial-assets", "sprx"));
        keyPath = Path.GetFullPath(config["SprxUpdates:SigningKeyPath"] ?? Path.Combine(env.ContentRootPath, "Private", "sprx-update-signing.pem"));
        publicKeyPath = Path.Combine(env.ContentRootPath, "Config", "sprx-update-public.pem");
    }
    public static bool Supported(string game, string region) => game != null && region != null && Regions.TryGetValue(game, out var allowed) && allowed.Contains(region, StringComparer.Ordinal);
    public sealed record Release(string Game, string Region, uint Revision, string FileName, byte[] Bytes, byte[] Digest);
    public Release Read(string game, string region, uint? revision = null)
    {
        if (!Supported(game, region) || !Directory.Exists(folder)) return null;
        var candidates = new List<(uint Revision, string Path)>();
        // A release can be archived during an in-progress download. Only an
        // explicitly pinned revision may read old/; manifests select live files.
        var paths = Directory.EnumerateFiles(folder, "*.sprx", SearchOption.TopDirectoryOnly);
        var archive = Path.Combine(folder, "old");
        if (revision.HasValue && Directory.Exists(archive) &&
            (File.GetAttributes(archive) & FileAttributes.ReparsePoint) == 0)
            paths = paths.Concat(Directory.EnumerateFiles(archive, "*.sprx", SearchOption.TopDirectoryOnly));
        foreach (var path in paths)
        {
            var match = FilePattern.Match(Path.GetFileName(path));
            if (!match.Success || match.Groups[1].Value != game || match.Groups[2].Value != region) continue;
            if (!uint.TryParse(match.Groups[3].Value, out var value) || value == 0 || (revision.HasValue && value != revision.Value)) continue;
            candidates.Add((value, path));
        }
        if (candidates.Count == 0) return null;
        var selected = candidates.OrderByDescending(x => x.Revision).First();
        // Refuse symlinks, overlarge/partial files, FSELF, wrong signing category
        // and a changed length. Never fall back silently to an older release.
        var info = new FileInfo(selected.Path);
        if ((info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length < 1024 || info.Length > MaximumFileSize) throw new InvalidDataException("Invalid SPRX release file.");
        using var stream = new FileStream(selected.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length != info.Length) throw new InvalidDataException("Release upload is in progress.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || bytes[0] != 'S' || bytes[1] != 'C' || bytes[2] != 'E' || bytes[3] != 0
            || bytes[8] != 0 || bytes[9] != 10 || bytes[10] != 0 || bytes[11] != 1) throw new InvalidDataException("Expected universal HEN/CFW signed APP SELF.");
        // SCE container length, including its metadata/header, must match.
        var headerLength = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(16, 8));
        var dataLength = BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(24, 8));
        if (headerLength > (ulong)bytes.Length || dataLength > (ulong)bytes.Length || headerLength + dataLength != (ulong)bytes.Length) throw new InvalidDataException("Incomplete SPRX container.");
        return new(game, region, selected.Revision, Path.GetFileName(selected.Path), bytes, SHA256.HashData(bytes));
    }
    public byte[] Manifest(Release release)
    {
        // Never generate a fresh key on a deployed server: installed consoles
        // trust exactly the public key compiled into the initial updater release.
        using var key = RSA.Create();
        key.ImportFromPem(File.ReadAllText(keyPath));
        using var expected = RSA.Create();
        expected.ImportFromPem(File.ReadAllText(publicKeyPath));
        if (key.KeySize != 2048 || !CryptographicOperations.FixedTimeEquals(key.ExportSubjectPublicKeyInfo(), expected.ExportSubjectPublicKeyInfo()))
            throw new InvalidDataException("SPRX update signing key does not match the installed client trust anchor.");
        var manifest = new byte[320];
        Encoding.ASCII.GetBytes("GRIMUPD1").CopyTo(manifest, 0);
        Encoding.ASCII.GetBytes(release.Game).CopyTo(manifest, 8);
        Encoding.ASCII.GetBytes(release.Region).CopyTo(manifest, 12);
        BinaryPrimitives.WriteUInt32BigEndian(manifest.AsSpan(24, 4), release.Revision);
        BinaryPrimitives.WriteUInt32BigEndian(manifest.AsSpan(28, 4), (uint)release.Bytes.Length);
        release.Digest.CopyTo(manifest, 32);
        key.SignData(manifest.AsSpan(0, 64), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).CopyTo(manifest, 64);
        return manifest;
    }
    public object[] Inventory() => Regions.SelectMany(g => g.Value.Select(region => new { game = g.Key, region })).Select(p =>
    {
        try { var r = Read(p.game, p.region); return (object)new { p.game, p.region, available = r != null, revision = r?.Revision, file = r?.FileName, sha256 = r == null ? null : Convert.ToHexString(r.Digest).ToLowerInvariant() }; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { return new { p.game, p.region, available = false, error = "Invalid or incomplete release file" }; }
    }).ToArray();
}
