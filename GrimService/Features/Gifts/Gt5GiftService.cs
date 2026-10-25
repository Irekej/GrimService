using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Features.Messaging;
using GrimService.Models.Xml;

namespace GrimService.Features.Gifts
{
    public interface IGt5GiftRepository
    {
        Task<IEnumerable<ItemBoxDTO>> ListAsync(string receiver, long cursor);
        Task<IEnumerable<ItemBoxDTO>> ListIdsAsync(string receiver);
        Task AddAsync(ItemBoxDTO item);
        Task<bool> ReceiveAsync(long id, string receiver);
    }

    public sealed class Gt5GiftRepository : IGt5GiftRepository
    {
        private readonly ItemBoxDBManager _items;
        public Gt5GiftRepository(ItemBoxDBManager items) => _items = items;
        public Task<IEnumerable<ItemBoxDTO>> ListAsync(string receiver, long cursor) => cursor == 0
            ? _items.GetAllItemsOfUser(receiver) : _items.GetItemOfUser(cursor, receiver);
        public Task<IEnumerable<ItemBoxDTO>> ListIdsAsync(string receiver) => _items.GetAllItemsOfUser2(receiver);
        public async Task AddAsync(ItemBoxDTO item) => await _items.AddAsync(item);
        public Task<bool> ReceiveAsync(long id, string receiver) => _items.ReceiveAsync(id, receiver);
    }

    public sealed class Gt5GiftService
    {
        private readonly IGt5GiftRepository _items;
        private readonly IRecipientDirectory _recipients;
        public Gt5GiftService(IGt5GiftRepository items, IRecipientDirectory recipients)
        { _items = items; _recipients = recipients; }

        public async Task<ItemBoxList> ListAsync(string receiver, long cursor)
        {
            if (cursor < 0) throw new ArgumentOutOfRangeException(nameof(cursor));
            var rows = await _items.ListAsync(receiver, cursor);
            return new ItemBoxList { Items = rows.Select(row => new ItemBox
            {
                ItemBoxId = row.Id, Sender = row.Sender, Receiver = row.Receiver, Type = row.Type,
                Stats = row.Stats, Comment = row.Comment, MacAddress = row.Mac, CreateTime = row.CreateTime
            }).ToList() };
        }

        public async Task<ItemBoxList> ListIdsAsync(string receiver) => new ItemBoxList
        { Items = (await _items.ListIdsAsync(receiver)).Select(row => new ItemBox { ItemBoxId = row.Id }).ToList() };

        public async Task<bool> SendAsync(int userId, string sender, string receiver, string type, string stats, string comment, string mac)
        {
            if (string.IsNullOrWhiteSpace(receiver) || await _recipients.FindAsync(receiver) == null) return false;
            await _items.AddAsync(new ItemBoxDTO(userId, sender, receiver, type, stats, comment ?? "", mac, DateTime.Now));
            return true;
        }

        public Task<bool> ReceiveAsync(long id, string receiver) => id <= 0
            ? Task.FromResult(false) : _items.ReceiveAsync(id, receiver);
    }
}
