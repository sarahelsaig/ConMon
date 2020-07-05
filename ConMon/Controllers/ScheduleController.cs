using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConMon.Classes;
using ConMon.Models;
using ConMon.Models.Scheduler;
using ConMon.Services;
using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using static ConMon.Controllers.ControllerExtensions;

namespace ConMon.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ScheduleController : ControllerBase
    {
        private static readonly string[] _unixShell = new[] { "/bin/sh", "/bin/bash", "/sbin/sh", "/sbin/bash", "/usr/bin/sh", "/usr/bin/bash" };

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
        private async Task AddToSchedule(ScheduleAddRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Program))
            {
                request.Program = _configuration.GetValue("DefaultApplication", @"c:\Windows\System32\cmd.exe");
                string args = request.Arguments.TrimStart();
                if (request.Program.EndsWith("cmd.exe") && !args.StartsWith("/C") && !args.StartsWith("/K"))
                    request.Arguments = $"/C {args.TrimEnd()}";
                else if (_unixShell.Contains(request.Program) && !args.StartsWith("-c"))
                    request.Arguments = $"-c {args.TrimEnd()}";
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
        public Task<ActionResult<object>> Add([FromBody] ScheduleAddRequest request) =>
            AttemptAsync(() => AddToSchedule(request));

        public Task<ActionResult<object>> AddMany([FromBody] IEnumerable<ScheduleAddRequest> requests) =>
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

        public ActionResult<ScheduleLinesResult> Lines(string label, int after = 0) =>
            ScheduleLinesResult.FromTuple(_applicationService.BufferGet(label, after));

        public ActionResult<string> Ip() => HttpContext.Connection.RemoteIpAddress.ToString();

        public ActionResult<IEnumerable<KeyValuePair<string, string[]>>> LogFiles(string label) =>
            LogsInfo.Create(_applicationService.FindByLabel(label).WorkingDirectory)
                .Groups
                .OrderBy(x => x.Key)
                .ToList();

        public ActionResult LogFile(string label, string file)
        {
            if (string.IsNullOrWhiteSpace(file)) return Content("File name is empty.");
            file = file.ToLower();
            
            var logsInfo = LogsInfo.Create(_applicationService.FindByLabel(label).WorkingDirectory);
            var fileInfo = logsInfo.Directory
                .GetFiles()
                .FirstOrDefault(x => x.Name.ToLower() == file);

            //System.IO.File.WriteAllText("log.txt", Newtonsoft.Json.JsonConvert.SerializeObject(new
            //{
            //    label,
            //    file,
            //    logsInfo = new { directory = logsInfo.Directory?.FullName, groups = logsInfo.Groups },
            //    fileInfo = fileInfo?.FullName
            //}));

            if (fileInfo?.Exists != true) return NotFound($"The file '{file}' is not found.");
            var bytes = System.IO.File.ReadAllBytes(fileInfo.FullName);
            return File(bytes, "text/plain");
        }
        #endregion
    }
}