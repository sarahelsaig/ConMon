using ConMon.Classes;
using ConMon.Models.Scheduler;
using ConMon.Services;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static ConMon.Controllers.ControllerExtensions;

namespace ConMon.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ScheduleController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, ProgramAlias> _programAliases;
        private readonly IApplicationService _applicationService;

        public ScheduleController(
            IConfiguration configuration,
            Dictionary<string, ProgramAlias> programAliases,
            IApplicationService applicationService)
        {
            _configuration = configuration;
            _programAliases = programAliases;
            _applicationService = applicationService;
        }

        /// <summary>
        /// Processes the request with default program value or program aliases, then registers it as a recurring job with the cron value.
        /// </summary>
        /// <param name="request">The request submitted for recurring job.</param>
        private async Task AddToSchedule(Models.ScheduleAddRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Program))
            {
                request.Program = _configuration.GetValue("DefaultApplication", @"c:\Windows\System32\cmd.exe");
                string args = request.Arguments.TrimStart();
                if (request.Program.EndsWith("cmd.exe") && !args.StartsWith("/C") && !args.StartsWith("/K"))
                    request.Arguments = $"/C {request.Arguments.Trim()}";
            }
            else if (_programAliases.ContainsKey(request.Program.Trim()))
                request.ApplyAlias(_programAliases[request.Program.Trim()]);

            await _applicationService.CreateAsync(request, null);
            string label = request.Label;

            RecurringJob.AddOrUpdate(
                request.Label,
                () => _applicationService.StartAsync(label, null),
                request.Cron);
            RecurringJob.Trigger(request.Label);
        }

        #region API
        public ActionResult<object> Add([FromBody] Models.ScheduleAddRequest request) =>
            AttemptAsync(() => AddToSchedule(request));

        public ActionResult<object> AddMany([FromBody] IEnumerable<Models.ScheduleAddRequest> requests) =>
            AttemptAsync(async () => { foreach (var request in requests) await AddToSchedule(request); });

        public ActionResult<IEnumerable<string>> Apps() =>
            new ActionResult<IEnumerable<string>>(JobStorage.Current.GetConnection().GetRecurringJobs().Select(x => x.Id));

        public ActionResult<bool> Running(string label)
        {
            label = label.ToLower();

            using var connection = JobStorage.Current.GetConnection();

            var jobs = JobStorage.Current.GetMonitoringApi().ProcessingJobs(0, 1000);
            return jobs.Any(x => connection.GetRecurringJobName(x.Key).ToLower() == label);
        }

        public ActionResult<object> Trigger(string label) =>
            Attempt(() => RecurringJob.Trigger(label));

        public ActionResult<object> Erase(string label) =>
            Attempt(() => _applicationService.BufferClear(label));

        public ActionResult<Models.ScheduleLinesResult> Lines(string label, int after = 0) =>
            Models.ScheduleLinesResult.FromTuple(_applicationService.BufferGet(label, after));
        #endregion
    }
}