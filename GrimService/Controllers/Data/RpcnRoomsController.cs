using GrimService.Services.Rpcn;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers
{
    [ApiController, Route("api/rpcn/rooms")]
    public sealed class RpcnRoomsController : ControllerBase
    {
        private readonly RpcnRoomsService rooms;
        public RpcnRoomsController(RpcnRoomsService rooms) => this.rooms = rooms;
        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult List() => Ok(rooms.Snapshot);
    }
}
