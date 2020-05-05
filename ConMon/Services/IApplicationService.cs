using ConMon.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConMon.Models.Scheduler;

namespace ConMon.Services
{
    public interface IApplicationService
    {
        Application FindByLabel(string label);
        
        void BufferClear(string label);
        (int, IEnumerable<string>) BufferGet(string label, int after = -1);

        Task CreateAsync(ScheduleAddRequest request, CancellationToken? optionalCancellationToken);
        Task StartAsync(string label, CancellationToken? optionalCancellationToken);
    }
}