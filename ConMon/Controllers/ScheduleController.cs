using ConMon.Classes;
using ConMon.Services;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using static ConMon.Controllers.ControllerExtensions;

namespace ConMon.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ScheduleController : ControllerBase
    {
        public static Dictionary<string, ProgramAlias> ProgramAliases;

        private void AddToSchedule(Models.ScheduleAddRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Program))
            {
                request.Program = @"c:\Windows\System32\cmd.exe";
                string args = request.Arguments.TrimStart();
                if (!args.StartsWith("/C") && !args.StartsWith("/K"))
                    request.Arguments = $"/C {request.Arguments.Trim()}";
            }
            else if (ProgramAliases.ContainsKey(request.Program.Trim()))
                request.ApplyAlias(ProgramAliases[request.Program.Trim()]);

            RecurringJob.AddOrUpdate(request.Label, () => ApplicationService.FromRequest(request).Start(null), request.Cron);
            RecurringJob.Trigger(request.Label);
        }

        public ActionResult<object> Add([FromBody] Models.ScheduleAddRequest request) =>
            Attempt(() => AddToSchedule(request));

        public ActionResult<object> AddMany([FromBody] IEnumerable<Models.ScheduleAddRequest> requests) =>
            Attempt(() => { foreach (var request in requests) AddToSchedule(request); });

        public ActionResult<IEnumerable<string>> Apps() =>
            new ActionResult<IEnumerable<string>>(JobStorage.Current.GetConnection().GetRecurringJobs().Select(x => x.Id));

        public ActionResult<bool> Running(string label)
        {
            label = label.ToLower();

            using (var connection = JobStorage.Current.GetConnection())
            {
                var jobs = JobStorage.Current.GetMonitoringApi().ProcessingJobs(0, 1000);
                return jobs.Any(x => connection.GetRecurringJobName(x.Key).ToLower() == label);
            }
        }

        public ActionResult<object> Trigger(string label) =>
            Attempt(() => RecurringJob.Trigger(label));

        public ActionResult<object> Erase(string label) =>
            Attempt(() => new ApplicationService(label).BufferClear());

        public ActionResult<Models.ScheduleLinesResult> Lines(string label, int after = 0) =>
            Models.ScheduleLinesResult.FromTuple(new ApplicationService(label).BufferGet(after));
    }
}