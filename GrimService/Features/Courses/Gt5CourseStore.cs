using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Tables;

namespace GrimService.Features.Courses;

public sealed class Gt5CourseStore(IDbConnection connection)
{
    public Task<CourseGT5DTO> Find(long id) => connection.QueryFirstOrDefaultAsync<CourseGT5DTO>("SELECT * FROM coursesgt5 WHERE id=@Id",new {Id=id});
    public Task<IEnumerable<CourseGT5DTO>> Owned(long owner) => connection.QueryAsync<CourseGT5DTO>("SELECT * FROM coursesgt5 WHERE user_id=@Owner ORDER BY id",new {Owner=owner});
    public Task<IEnumerable<CourseGT5DTO>> Named(string name) => connection.QueryAsync<CourseGT5DTO>("SELECT * FROM coursesgt5 WHERE psnuserid=@Name",new {Name=name});
    public async Task<bool> SetPhoto(long id,int owner,long photo) => await connection.ExecuteAsync(
        "UPDATE coursesgt5 SET photo_id=@Photo,update_time=@Now WHERE id=@Id AND user_id=@Owner",
        new {Id=id,Owner=owner,Photo=photo.ToString(CultureInfo.InvariantCulture),Now=DateTime.UtcNow})==1;
    public async Task Replace(CourseGT5DTO course) => await connection.ExecuteAsync(@"UPDATE coursesgt5 SET title=@Title,
        comment=@Comment,genre=@Genre,length=@Length,weather=@Weather,start_datetime=@StartDatetime,
        elevation=@Elevation,straight=@Straight,corner=@Corner,datetime=@Datetime,online_race=@OnlineRace,
        weather_value0=@WeatherValue0,initial_retention=@InitialRetention,weather_accel=@WeatherAccel,
        time_progress_speed=@TimeProgressSpeed,photo_id=@PhotoId,update_time=@UpdateTime
        WHERE id=@Id AND user_id=@UserId",course);
    public Task<long> Append(CourseGT5DTO course) => connection.ExecuteScalarAsync<long>(@"INSERT INTO coursesgt5
        (user_id,psnuserid,title,comment,genre,length,weather,start_datetime,author,elevation,straight,corner,
            datetime,online_race,weather_value0,initial_retention,weather_accel,time_progress_speed,create_time,update_time)
        VALUES (@UserId,@PSNUserId,@Title,@Comment,@Genre,@Length,@Weather,@StartDatetime,@Author,@Elevation,
            @Straight,@Corner,@Datetime,@OnlineRace,@WeatherValue0,@InitialRetention,@WeatherAccel,@TimeProgressSpeed,@CreateTime,@UpdateTime)
        RETURNING id",course);
    public async Task Delete(long id) => await connection.ExecuteAsync("DELETE FROM coursesgt5 WHERE id=@Id",new {Id=id});
    public async Task DeleteOwned(long owner,long id) => await connection.ExecuteAsync("DELETE FROM coursesgt5 WHERE user_id=@Owner AND id=@Id",new {Owner=owner,Id=id});
    public void EnsureSchema() => connection.Execute(@"CREATE TABLE IF NOT EXISTS coursesgt5 (
        id SERIAL PRIMARY KEY,user_id BIGINT REFERENCES users(id),psnuserid TEXT,title TEXT,comment TEXT,
        genre TEXT,length TEXT,weather TEXT,start_datetime TEXT,author TEXT,elevation TEXT,straight TEXT,
        corner TEXT,datetime TEXT,online_race TEXT,weather_value0 TEXT,initial_retention TEXT,weather_accel TEXT,
        time_progress_speed TEXT,photo_id TEXT,update_time TIMESTAMP WITHOUT TIME ZONE,create_time TIMESTAMP WITHOUT TIME ZONE);
        CREATE INDEX IF NOT EXISTS coursesgt5_user_id_idx ON coursesgt5(user_id)");
}
