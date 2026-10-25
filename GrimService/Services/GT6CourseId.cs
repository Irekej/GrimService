using System;

namespace GrimService.Services
{


    public static class GT6CourseId
    {
        public const long Offset = 1000000;
        public static long ToClient(long id) => id > 0 ? checked(id + Offset) : id;

        public static long ToDatabase(long id) => id >= Offset ? id - Offset : id;
    }
}
