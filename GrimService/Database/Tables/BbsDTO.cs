using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class BbsDTO
    {
        public int Id { get; set; }




        public int BbsBoardId { get; set; }




        public int AuthorId { get; set; }

        public string Comment { get; set; }

        public DateTime CreateTime { get; set; }

        public BbsDTO() { }

        public BbsDTO(int boardId, int authorId, string comment, DateTime createTime)
        {
            BbsBoardId = boardId;
            AuthorId = authorId;
            Comment = comment;
            CreateTime = createTime;
        }
    }
}
