using System;
using System.Threading.Tasks;
using GrimService.Database.Controllers;

namespace GrimService.Features.Clubs
{
    public enum ClubTemplateOperation { Create, Update, Delete }
    public interface IClubTemplateRepository
    {
        Task<long> SaveAsync(long clubId, long userId, long templateId, string description, string regulation, bool delete);
    }

    public sealed class ClubTemplateRepository : IClubTemplateRepository
    {
        private readonly GT6SocialDBManager social;
        public ClubTemplateRepository(GT6SocialDBManager social) => this.social = social;
        public Task<long> SaveAsync(long clubId, long userId, long templateId, string description, string regulation, bool delete) =>
            social.SaveTemplateAsync(clubId, userId, templateId, description, regulation, delete);
    }

    public sealed class ClubTemplateService
    {
        private readonly IClubTemplateRepository repository;
        public ClubTemplateService(IClubTemplateRepository repository) => this.repository = repository;
        public async Task<ClubEventWriteResult> SaveAsync(long clubId, long templateId, ClubEventEditor editor,
            ClubTemplateOperation operation, string description, string regulation)
        {
            if (editor.Game != "GT6") return new(ClubEventWriteStatus.Forbidden);
            if (!Enum.IsDefined(operation) || (operation != ClubTemplateOperation.Create && templateId <= 0))
                return new(ClubEventWriteStatus.Invalid);
            if (!editor.IsOwner) return new(ClubEventWriteStatus.Forbidden);
            description ??= "";
            bool delete = operation == ClubTemplateOperation.Delete;
            if (!delete && (!ClubText.IsValid(description, 140) || !ClubText.IsValid(regulation, 1000000, true)))
                return new(ClubEventWriteStatus.Invalid);
            long saved = await repository.SaveAsync(clubId, editor.UserId,
                operation == ClubTemplateOperation.Create ? 0 : templateId, description, regulation, delete);
            return new(ClubEventWriteStatus.Saved, saved < 0 ? -1 : operation == ClubTemplateOperation.Create ? saved : 1);
        }
    }
}
