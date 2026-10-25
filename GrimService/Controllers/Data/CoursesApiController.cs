using GrimService.Config;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using static GrimService.Controllers.DiscordApiController;

namespace GrimService.Controllers
{

    [ApiController]
    public class CoursesApiController : ControllerBase
    {
        private readonly ILogger<CoursesApiController> _logger;
        private readonly UserDBManager _users;
        private readonly CourseDBManager _courses;

        public CoursesApiController(ILogger<CoursesApiController> logger, UserDBManager users, CourseDBManager courses)
        {
            _logger = logger;
            _users = users;
            _courses = courses;
        }

        [HttpPost]
        [Route("api/courses/upload-xml/kekekeke/api/bobr")]
        public async Task<IActionResult> AddCourse([FromBody] UploadCourseRequest requestCourse)
        {
            if (ModelState.IsValid)
            {
                var existingPlayer = await _users.GetUserByDiscordId(requestCourse.DiscordId);
                if (existingPlayer == null)
                {
                    return Ok($"Player not found. Try link your discord account with GT PSN account. (>discordlink PSN)");
                }

                string statusexist = "0";

                var existingCoursenotFinished = await _courses.GetAllCoursesOfUserWithStatus(existingPlayer.PSNUserId, statusexist);
                if (existingCoursenotFinished != null)
                {
                    foreach (var courseexist in existingCoursenotFinished)
                    {
                        _logger.LogDebug("[{name}] Log: Player already uploaded un-complete course, removing previous course from database ID: {courseexist} ", existingPlayer.PSNUserId, courseexist.Id);
                        await _courses.RemoveAsync(courseexist.Id);
                    }
                }


                long UserId = existingPlayer.Id;
                string Status = "0";
                string Title = requestCourse.Title;
                string OwnerId = existingPlayer.PSNUserId;
                string SourceUserId = existingPlayer.PSNUserId;


                CourseDTO course = new CourseDTO(UserId, Status, Title, OwnerId, SourceUserId, DateTime.Now);
                long courseId = await _courses.AddAsync(course);

                _logger.LogDebug("[{name}] Log: Player uploaded a course XML to the server ID: {courseId} ", existingPlayer.PSNUserId, courseId);

                return Ok(courseId);
            }

            return BadRequest(ModelState);
        }

        [HttpPost]
        [Route("api/courses/upload-file/irekekekekeapi/bobr")]
        public async Task<IActionResult> UploadCourseFile([FromForm] IFormFile file)
        {
            if (file != null && file.Length > 0 && file.Length < 250000)
            {
                /*
                var existingPlayer = await _users.GetUserByDiscordId(discordId);
                if (existingPlayer == null)
                {
                    return Ok($"Player not found. Try linking your Discord account with GT PSN account. (>discordlink PSN)");
                }
                */

                var courseIdStr = Request.Headers["CourseId"].ToString();
                if (!long.TryParse(courseIdStr, out var courseId))
                {
                    return Ok("Course not found. Try link your discord account with GT PSN account. (>discordlink PSN)");
                }

                var existingCourseId = await _courses.GetByIDAsync(courseId);
                if (existingCourseId == null)
                {
                    return Ok($"Course data not found. Contact irekej.");
                }

                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources/course/data");
                Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, $"{courseId}.dat");

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                _logger.LogDebug("[{name}] Log: Player uploaded a course file to the server. Course ID: {courseId} ", existingCourseId.OwnerId, courseId);

                return Ok($"Successfully uploaded a course. PSN User ID: {existingCourseId.OwnerId}");
            }

            return Ok("Invalid file or file is empty.");
        }

        [XmlRoot("UploadCourseRequest")]
        public class UploadCourseRequest
        {
            [XmlElement("Title")]
            public string Title { get; set; }

            [XmlElement("DiscordId")]
            public string DiscordId { get; set; }
        }
    }
}
