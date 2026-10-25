using System.Collections.Generic;
using System.Threading.Tasks;
using GrimService.Services;

namespace GrimService.Features.Moderation
{
    public sealed record ReportSubmission(string Game, long ReporterId, string Reporter,
        string Target, string Payload, string Fingerprint);

    public interface IReportRepository
    {
        void CreateTable();
        Task<long> SubmitAsync(ReportSubmission report);
        Task<IEnumerable<ModerationReport>> ListAsync(string game, int page);
        Task<bool> ReviewAsync(long id, int version, string status, string note, string actor);
        Task<IEnumerable<ReportDecision>> HistoryAsync(long id);
    }
}
