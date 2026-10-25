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
    public class EventsApiController : ControllerBase
    {
        private readonly ILogger<EventsApiController> _logger;
        private readonly UserDBManager _users;
        private readonly CourseDBManager _courses;

        public EventsApiController(ILogger<EventsApiController> logger, UserDBManager users, CourseDBManager courses)
        {
            _logger = logger;
            _users = users;
            _courses = courses;
        }

        [HttpPost]
        [Route("api/events/upload-event-gt6/irekekekekeapi/bobr")]
        public async Task<IActionResult> UploadCourseFile([FromForm] IFormFile file)
        {
            if (file != null && file.Length > 0 && file.Length < 1000000)
            {
                var eventIdStr = Request.Headers["FileName"].ToString();



                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "Resources/event/dev/GT6");
                Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, $"{eventIdStr}");

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                return Ok($"Successfully uploaded a event. File name: {eventIdStr}");
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
