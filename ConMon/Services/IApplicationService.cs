using ConMon.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ConMon.Services
{
    public interface IApplicationService
    {
        void BufferClear(string label);
        (int, IEnumerable<string>) BufferGet(string label, int after = -1);

        Task CreateAsync(ScheduleAddRequest request, CancellationToken? optionalCancellationToken);
        Task StartAsync(string label, CancellationToken? optionalCancellationToken);
    }
}