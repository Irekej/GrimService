using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using GrimService.Models.Xml;
using GrimService.Features.Moderation;

namespace GrimService.Services
{
    public sealed class ModerationReport
    {
        public long Id { get; set; }
        public string Game { get; set; }
        public string Reporter { get; set; }
        public string Target { get; set; }
        public string Payload { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class ReportService
    {
        private readonly IReportRepository repository;
        public ReportService(IReportRepository repository) => this.repository = repository;
        public void CreateTable() => repository.CreateTable();
        public static SortedDictionary<string, string> Parse(GrimRequest request) => ReportRequestParser.Parse(request);

        public Task<long> Submit(string game, long reporterId, string reporter, GrimRequest request)
        {
            ValidateGame(game);
            var fields = Parse(request);
            var payload = JsonSerializer.Serialize(fields);
            var target = fields[request.Command == "report.send" ? "target_user_id" : "user_id"];
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
            return repository.SubmitAsync(new ReportSubmission(game, reporterId, reporter, target, payload, fingerprint));
        }

        public Task<IEnumerable<ModerationReport>> List(string game, int page)
        {
            ValidateGame(game);
            return repository.ListAsync(game, Math.Clamp(page, 1, 100000));
        }

        public Task<bool> Review(long id, int version, string status, string note, string actor)
        {
            if (!new[] { "open", "reviewing", "resolved", "dismissed" }.Contains(status) || (note?.Length ?? 0) > 4000)
                throw new ArgumentException("Invalid moderation decision.");
            return repository.ReviewAsync(id, version, status, note ?? "", actor);
        }

        public Task<IEnumerable<ReportDecision>> History(long id) => repository.HistoryAsync(id);
        private static void ValidateGame(string game)
        {
            if (game != "GT5" && game != "GT6") throw new ArgumentException("Unknown game.");
        }
    }
    public sealed class ReportDecision
    {
        public string Actor { get; set; }
        public string OldStatus { get; set; }
        public string NewStatus { get; set; }
        public string OldNote { get; set; }
        public string NewNote { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
