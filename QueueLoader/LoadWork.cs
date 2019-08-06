using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace QueueLoader
{
    public static class LoadWork
    {
        private const int MaxBatchSize = 2000;
        private static readonly byte[] MessageBody = Encoding.UTF8.GetBytes(@"{""message"":""text""}");

        [FunctionName("LoadWork")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")]
            HttpRequest req,
            ILogger log)
        {
            try
            {
                string qtyParam = req.Query["qty"];

                if (qtyParam == null || !int.TryParse(qtyParam, out var qty))
                    return new BadRequestObjectResult("Please supply a numeric qty parameter");

                var queueClient = new QueueClient(Environment.GetEnvironmentVariable("sbConnectionString"),
                    Environment.GetEnvironmentVariable("workQueue"));

                while (qty > 0)
                {
                    var batchSize = Math.Min(qty, MaxBatchSize);
                    var batch = Enumerable.Range(0, batchSize).Select(_ => new Message(MessageBody))
                        .ToList();
                    await queueClient.SendAsync(batch);
                    qty -= batchSize;
                }

                return new OkObjectResult("Added messages to input queue");
            }
            catch (Exception exc)
            {
                return new OkObjectResult(exc.ToString());
            }
        }
    }
}