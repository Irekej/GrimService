namespace GrimService.Services
{
    public sealed class SeasonalPublicEvent
    {
        public int Folder { get; set; }
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Begin { get; set; }
        public string End { get; set; }
        public string Mode { get; set; }
        public string Category { get; set; }
        public string Course { get; set; }
        public string BoardId { get; set; }
        public string[] Parameters { get; set; }
    }
}
