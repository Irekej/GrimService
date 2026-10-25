namespace GrimService.Features.Clubs
{
    public static class ClubIdentity
    {
        public const long Offset = 1000000;
        public static long ToDatabase(long id) => id >= Offset ? id - Offset : id;
        public static long ToPublic(long id) => id + Offset;
    }
}
