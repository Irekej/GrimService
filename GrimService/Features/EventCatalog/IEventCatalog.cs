namespace GrimService.Features.EventCatalog
{

    public interface IEventCatalog
    {
        EventResource FindList(string server, string lastGame);
        EventResource FindEvent(string server, string lastGame, int folderId);
        EventResource FindSettings(string server, string lastGame);
        EventResource FindImage(string server, string fileName);
    }
}
