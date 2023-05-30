using System.Diagnostics.Tracing;

namespace Metrics;

class Program
{
    static void Main(string[] args)
    {
        //var reader = new CustomMetricsEventListener();
        //var arguments = new Dictionary<string, string?>
        //{
        //    {"EventCounterIntervalSec", "1"}
        //};


        //TimingMetricsEventSource
        //                .Logger    
        //                .AddEventCounters
        //                            (
        //                                new EventCounter("sleep1", TimingMetricsEventSource.Logger)
        //                                { 
        //                                     DisplayName = "sleep1"
        //                                     , DisplayUnits = "ms/op"

        //                                }
        //                                , new EventCounter("sleep2", TimingMetricsEventSource.Logger)
        //                                {
        //                                    DisplayName = "sleep2"
        //                                     ,
        //                                    DisplayUnits = "ms/op"

        //                                }

        //                            );


        //reader.EnableEvents(customMetricsEventSource, EventLevel.LogAlways, EventKeywords.All, arguments);

        CommonMetricsEventSource.Logger.AddCounters("sleep1");
        //CommonMetricsEventSource.Logger.AddCounters("sleep2");

        var random = new Random();

        var cts = new CancellationTokenSource();

        Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                //SleepingBeauty(random.Next(10, 20),1);
                //SleepingBeauty(random.Next(10, 20), 2);
                SleepingBeauty(100, 1);
                //SleepingBeauty(5, 2);

                //Thread.Sleep(5 * 1000);
            }
        });

        Console.WriteLine("Press any key to stop");
        Console.ReadKey();

        cts.Cancel();

        //customMetricsEventSource.ApplicationStop();
    }

    static void SleepingBeauty(int sleepTimeInMs, int s)
    {
        //var timeStamp = TimingMetricsEventSource.Logger.StartTiming();
        var countersNamePrefix = $"sleep{s}";
        var timestamp = CommonMetricsEventSource.Logger.StartCounting(countersNamePrefix);
        Thread.Sleep(sleepTimeInMs);
        //TimingMetricsEventSource.Logger.StopTiming($"sleep{s}", timeStamp);
        CommonMetricsEventSource.Logger.StopCounting(countersNamePrefix, timestamp);
    }
}