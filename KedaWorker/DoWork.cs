using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace KedaWorker
{
    public static class DoWork
    {
        private static readonly Guid InstanceId = Guid.NewGuid();

        [FunctionName("DoWork")]
        public static async Task Run([ServiceBusTrigger("%workQueue%", Connection = "sbConnectionString")]
            string work,
            [ServiceBus("%outputQueue%", Connection = "sbConnectionString")]
            ICollector<string> output,
            ILogger log)
        {
            // TODO: Mine bitcoin
            await Task.Delay(TimeSpan.FromSeconds(1));

            var outputMessage = new JObject {["instanceId"] = InstanceId}.ToString();
            output.Add(outputMessage);
            log.LogInformation(outputMessage);
        }
    }
}