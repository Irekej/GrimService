using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Models.Xml;

namespace GrimService.Features.PlayerRecords;

public interface IPlayerBoardRepository
{
    Task<bool> BoardExists(int boardId);
    Task<string> AuthorName(int userId);
    Task<IEnumerable<BbsDTO>> Read(int boardId);
    Task<BbsDTO> Find(int commentId);
    Task Append(BbsDTO comment);
    Task Delete(int commentId);
}

public sealed class PlayerBoardRepository(UserDBManager users, BbsBoardDBManager boards) : IPlayerBoardRepository
{
    public async Task<bool> BoardExists(int boardId) => await users.GetByIDAsync(boardId) != null;
    public Task<string> AuthorName(int userId) => users.GetPSNNameByIdAsync(userId);
    public Task<IEnumerable<BbsDTO>> Read(int boardId) => boards.GetAllCommentsOfBoard(boardId);
    public Task<BbsDTO> Find(int commentId) => boards.GetByIDAsync(commentId);
    public async Task Append(BbsDTO comment) => await boards.AddAsync(comment);
    public Task Delete(int commentId) => boards.RemoveAsync(commentId);
}

public enum BoardStatus { Success, InvalidRequest, Forbidden, Unsupported }
public readonly record struct BoardReply(BoardStatus Status, BbsCommentList Comments = null);

public sealed class PlayerBoardService(IPlayerBoardRepository repository, TimeProvider clock)
{
    public async Task<BoardReply> Execute(int userId, string userName, GrimRequest request)
    {
        if (request.Command == "bbs.deleteComment")
        {
            if (!request.TryGetParameterIntByKey("bbs_comment_id", out int commentId))
                return new(BoardStatus.InvalidRequest);
            var comment = await repository.Find(commentId);
            if (comment == null || (comment.BbsBoardId != userId && comment.AuthorId != userId))
                return new(BoardStatus.Forbidden);
            await repository.Delete(commentId);
            return new(BoardStatus.Success);
        }

        if (request.Command != "bbs.getCommentList" && request.Command != "bbs.updateComment")
            return new(BoardStatus.Unsupported);
        if (!request.TryGetParameterIntByKey("bbs_board_id", out int boardId))
            return new(BoardStatus.InvalidRequest);

        if (request.Command == "bbs.updateComment")
        {
            if (!request.TryGetParameterByKey("comment", out var text) || text.Text.Length is < 1 or > 140)
                return new(BoardStatus.InvalidRequest);
            if (!await repository.BoardExists(boardId))
                return new(BoardStatus.InvalidRequest);
            await repository.Append(new BbsDTO(boardId, userId, text.Text,
                DateTime.SpecifyKind(clock.GetLocalNow().DateTime, DateTimeKind.Local)));
            return new(BoardStatus.Success);
        }

        if (!request.TryGetParameterIntByKey("bbs_comment_id", out int cursor) || !await repository.BoardExists(boardId))
            return new(BoardStatus.InvalidRequest);
        var list = new BbsCommentList();
        foreach (var row in await repository.Read(boardId))
        {
            if (row.Id <= cursor) continue;
            var name = row.AuthorId == userId ? userName : await repository.AuthorName(row.AuthorId);

            if (name == null) break;
            list.Comments.Add(new BbsComment {
                BoardId = boardId, CommentId = row.Id, Comment = row.Comment,
                CreateTime = row.CreateTime, Nickname = name, UserId = name
            });
        }
        return new(BoardStatus.Success, list);
    }
}
