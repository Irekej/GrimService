using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Data;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Dapper;
using Npgsql;

using Microsoft.Extensions.DependencyInjection;
using GrimService.Database.Tables;

namespace GrimService.Database.Controllers
{
    public class CourseDBManager : IDBManager<CourseDTO>
    {
        private ILogger<CourseDBManager> _logger;
        protected IDbConnection _con;
        private readonly string _connectionString;

        public CourseDBManager(ILogger<CourseDBManager> logger, IDbConnection con)
        {
            _logger = logger;
            _con = con;
            _connectionString = con.ConnectionString;
        }

        public async Task<CourseDTO> GetByIDAsync(long id)
            => await _con.QueryFirstOrDefaultAsync<CourseDTO>(@"SELECT * FROM courses WHERE id = @Id", new { Id = id });






        public async Task<IEnumerable<CourseDTO>> GetAllCoursesOfUser(string PSNUserId)
            => await _con.QueryAsync<CourseDTO>(@"SELECT * FROM courses WHERE owner_id=@psnuserid", new { psnuserid = PSNUserId });

        public async Task<IEnumerable<CourseDTO>> GetAllCoursesOfUserWithStatus(string PSNUserId, string Status)
            => await _con.QueryAsync<CourseDTO>(@"SELECT * FROM courses WHERE owner_id=@psnuserid AND status=@status ", new { psnuserid = PSNUserId, status = Status });

        public async Task<IEnumerable<CourseDTO>> GetAllForAdminAsync(long userId)
            => await _con.QueryAsync<CourseDTO>(@"SELECT * FROM courses WHERE user_id = @UserId ORDER BY create_time DESC NULLS LAST", new { UserId = userId });

        public async Task UpdateCourseData(long id, string title, string comment, string theme, string length, string corners, string oneway, string straight, string height, string status, long photoId)
            => await _con.ExecuteAsync(@"UPDATE courses SET comment=@Comment, height=@Height, one_way=@OneWay, status=@Status, straight=@Straight, title=@Title, theme=@Theme, length=@Length, corners=@Corners, photo_id=@PhotoId, update_time=LOCALTIMESTAMP WHERE id = @Id", new { Comment = comment, Height = height, OneWay = oneway, Status = status, Straight = straight, Title = title, Theme = theme, Length = length, Corners = corners, PhotoId = photoId, Id = id });

        public async Task UpdateCourseStatus(long id, string status)
            => await _con.ExecuteAsync(@"UPDATE courses SET status=@Status, update_time=LOCALTIMESTAMP WHERE id = @Id", new { Status = status, Id = id });

        public async Task UpdateCourseTitleComment(long id, string title, string comment)
            => await _con.ExecuteAsync(@"UPDATE courses SET title=@Title, comment=@Comment, update_time=LOCALTIMESTAMP WHERE id = @Id", new { Title = title, Comment = comment, Id = id });

        public async Task UpdateCourseTitle(long id, string title)
            => await _con.ExecuteAsync(@"UPDATE courses SET title=@Title, update_time=LOCALTIMESTAMP WHERE id = @Id", new { Title = title, Id = id });

        public async Task UpdateCourseComment(long id, string comment)
            => await _con.ExecuteAsync(@"UPDATE courses SET comment=@Comment, update_time=LOCALTIMESTAMP WHERE id = @Id", new { Comment = comment, Id = id });


        public async Task UpdateAsync(CourseDTO pData)
            => await _con.ExecuteAsync(@"UPDATE courses SET comment=@Comment, height=@Height, one_way=@OneWay, status=@Status, straight=@Straight, title=@Title, theme=@Theme, length=@Length, owner_id=@OwnerId, corners=@Corners, photo_id=CAST(@PhotoId AS text), photo_hidden=@PhotoHidden, comment_hidden=@CommentHidden, title_hidden=@TitleHidden, update_time=@UpdateTime, transfer_time=@TransferTime WHERE id=@Id AND user_id=@UserId", pData);

        public async Task<long> AddAsync(CourseDTO pData)
        {
            var query =
@"INSERT INTO courses (user_id, status, title, owner_id, source_user_id, create_time)
  VALUES
(@UserId, @Status, @Title, @OwnerId, @SourceUserId, @CreateTime)
  returning id";

            return await _con.ExecuteScalarAsync<long>(query, new { pData.UserId, pData.Status, pData.Title, pData.OwnerId, pData.SourceUserId, pData.CreateTime});
        }

        public async Task RemoveAsync(long id)
            => await _con.ExecuteAsync(@"DELETE FROM courses WHERE id=@Id", new { Id = id });


        public async Task<long?> CopyAsync(long sourceId, string sourceOwner, long userId, string owner, string resourceRoot)
        {
            using var con = new NpgsqlConnection(_connectionString);
            await con.OpenAsync();
            using var tx = con.BeginTransaction();
            var source = await con.QueryFirstOrDefaultAsync<CourseDTO>(
                @"SELECT * FROM courses WHERE id=@sourceId AND owner_id=@sourceOwner FOR SHARE",
                new { sourceId, sourceOwner }, tx);

            if (source == null || (source.UserId != userId && source.Status != "2")) return null;
            var root = Path.GetFullPath(resourceRoot);
            var dataDir = Path.Combine(root, "course", "data");
            var imageDir = Path.Combine(root, "photo", "image");
            var sourceData = Path.Combine(dataDir, $"{source.Id}.dat");
            if (!File.Exists(sourceData)) throw new FileNotFoundException("Source course data is missing.", sourceData);
            var images = new List<(string Path, string Suffix)>();
            if (source.PhotoId > 0)
            {
                foreach (var suffix in new[] { "_0.png", "_1.png", ".png" })
                {
                    var path = Path.Combine(imageDir, source.PhotoId + suffix);
                    if (File.Exists(path)) images.Add((path, suffix));
                }
                if (images.Count == 0) throw new FileNotFoundException("Source course photo is missing.");
            }
            var created = new List<string>();
            bool commitAttempted = false;
            try
            {
                long photoId = 0;
                if (images.Count > 0)
                {
                    var photo = await con.QueryFirstOrDefaultAsync<PhotoDTO>(
                        "SELECT * FROM photos WHERE id=@Id FOR SHARE", new { Id = source.PhotoId }, tx);
                    photoId = await con.ExecuteScalarAsync<long>(@"
                        INSERT INTO photos(user_id,username,comment,car_name,place,type,create_time)
                        VALUES(@userId,@Username,@Comment,@CarName,@Place,@Type,@CreateTime) RETURNING id",
                        new { userId, Username = photo?.Username ?? source.SourceUserId ?? source.OwnerId,
                            Comment = photo?.Comment ?? source.Comment, CarName = photo?.CarName ?? source.Title,
                            Place = photo?.Place ?? "course", Type = photo?.Type ?? "15",
                            CreateTime = photo?.CreateTime ?? source.CreateTime }, tx);
                }

                var id = await con.ExecuteScalarAsync<long>(@"
                    INSERT INTO courses(user_id,comment,height,one_way,status,straight,title,theme,length,
                        owner_id,corners,source_user_id,photo_id,photo_hidden,comment_hidden,title_hidden,
                        update_time,create_time,transfer_time)
                    SELECT @userId,comment,height,one_way,'1',straight,title,theme,length,
                        @owner,corners,source_user_id,CAST(@photoId AS text),photo_hidden,comment_hidden,title_hidden,
                        update_time,create_time,LOCALTIMESTAMP FROM courses WHERE id=@sourceId RETURNING id",
                    new { userId, owner, photoId, sourceId }, tx);
                await CopyFile(sourceData, Path.Combine(dataDir, $"{id}.dat"));
                foreach (var image in images)
                    await CopyFile(image.Path, Path.Combine(imageDir, photoId + image.Suffix));
                commitAttempted = true;
                tx.Commit();
                return id;
            }
            catch
            {


                if (!commitAttempted)
                    foreach (var path in created)
                        try { File.Delete(path); }
                        catch (Exception ex) { _logger.LogError(ex, "Could not clean up failed course copy: {Path}", path); }
                throw;
            }

            async Task CopyFile(string from, string to)
            {
                using var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var output = new FileStream(to, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                created.Add(to);
                await input.CopyToAsync(output);
            }
        }

        public async Task RemoveForAdminAsync(long userId, long courseId)
            => await _con.ExecuteAsync(@"DELETE FROM courses WHERE user_id = @UserId AND id = @Id", new { UserId = userId, Id = courseId });







        public async Task RemoveCourseAsync(long userId, long courseId)
           => await _con.ExecuteAsync(@"DELETE FROM courses WHERE user_id=@UserId AND id=@Id", new { UserId = userId, Id = courseId });

        public void CreateTable()
        {
            string query =
            @"CREATE TABLE IF NOT EXISTS courses (
                id SERIAL PRIMARY KEY,
				user_id BIGINT REFERENCES users(id),
                comment TEXT,
                height TEXT,
                one_way TEXT,
                status TEXT,
                straight TEXT,
                title TEXT,
                theme TEXT,
                length TEXT,
                owner_id TEXT,
                corners TEXT,
                source_user_id TEXT,
                photo_id TEXT,
                photo_hidden TEXT,
                comment_hidden TEXT,
                title_hidden TEXT,
                update_time TIMESTAMP WITHOUT TIME ZONE,
                create_time TIMESTAMP WITHOUT TIME ZONE,
                transfer_time TIMESTAMP WITHOUT TIME ZONE
			);";

            _con.Execute(query);

            string query2 = @"CREATE INDEX IF NOT EXISTS courses_user_id_idx ON courses (user_id)";
            _con.Execute(query2);
        }
    }
}
