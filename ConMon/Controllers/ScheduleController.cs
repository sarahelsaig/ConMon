using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Hangfire;
using System.Threading;
using ConMon.Services;
using System.Data.SqlClient;
using Hangfire.Storage;

namespace ConMon.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ScheduleController : ControllerBase
    {

        public ActionResult<object> Add([FromBody] Models.ScheduleAddRequest request)
        {
            try
            {
                RecurringJob.AddOrUpdate(request.Label, () => ApplicationService.FromRequest(request).Start(null), request.Cron);
                RecurringJob.Trigger(request.Label);

                return true;
            }
            catch(Exception e)
            {
                return e;
            }
        }

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

        public ActionResult<object> Trigger(string label)
        {
            try
            {
                RecurringJob.Trigger(label);
                return true;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        public ActionResult<object> Erase(string label)
        {
            try
            {
                new ApplicationService(label).BufferClear();
                return true;
            }
            catch (Exception e)
            {
                return e;
            }
        }

        public ActionResult<Models.ScheduleLinesResult> Lines(string label, int after = 0) =>
            Models.ScheduleLinesResult.FromTuple(new ApplicationService(label).BufferGet(after));
    }
}