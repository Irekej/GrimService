using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using GrimService.Database.Tables;
using GrimService.Features.Courses;
using Microsoft.Extensions.Logging;

namespace GrimService.Database.Controllers;

public class CourseGT5DBManager : IDBManager<CourseGT5DTO>
{
    protected IDbConnection _con;
    private readonly Gt5CourseStore store;
    public CourseGT5DBManager(ILogger<CourseGT5DBManager> logger,IDbConnection con) { _con=con;store=new(con); }
    public Task<CourseGT5DTO> GetByIDAsync(long id)=>store.Find(id);
    public Task<IEnumerable<CourseGT5DTO>> GetAllCoursesOfUser(long id)=>store.Owned(id);
    public Task<bool> SetPhotoAsync(long courseId,int ownerId,long photoId)=>store.SetPhoto(courseId,ownerId,photoId);
    public Task<IEnumerable<CourseGT5DTO>> GetAllCoursesOfPSNUserId(string username)=>store.Named(username);
    public Task UpdateAsync(CourseGT5DTO pData)=>store.Replace(pData);
    public Task<long> AddAsync(CourseGT5DTO pData)=>store.Append(pData);
    public Task RemoveAsync(long id)=>store.Delete(id);
    public Task RemoveCourseAsync(long userId,long courseId)=>store.DeleteOwned(userId,courseId);
    public void CreateTable()=>store.EnsureSchema();
}
