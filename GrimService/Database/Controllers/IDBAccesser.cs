using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace GrimService.Database.Controllers
{
    interface IDBManager<T>
    {





        Task<long> AddAsync(T t);





        Task RemoveAsync(long t);






        Task<T> GetByIDAsync(long id);





        Task UpdateAsync(T t);
    }
}
