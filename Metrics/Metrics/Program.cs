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

        TimingMetricsEventSource source = new TimingMetricsEventSource();

        TimingMetricsEventSource
                        //.Log    
                        .AddEventCounters
                                    (
                                        new EventCounter("sleep1", source)
                                        { 
                                             DisplayName = "sleep1"
                                             , DisplayUnits = "ms/op"
                            
                                        }
                                        , new EventCounter("sleep2", source)
                                        {
                                            DisplayName = "sleep2"
                                             ,
                                            DisplayUnits = "ms/op"

                                        }

                                    );


        //reader.EnableEvents(customMetricsEventSource, EventLevel.LogAlways, EventKeywords.All, arguments);

        var random = new Random();

        var cts = new CancellationTokenSource();

        Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                SleepingBeauty(random.Next(10, 20),1);
                SleepingBeauty(random.Next(10, 20), 2);

                Thread.Sleep(5 * 1000);
            }
        });

        Console.WriteLine("Press any key to stop");
        Console.ReadKey();

        cts.Cancel();

        //customMetricsEventSource.ApplicationStop();
    }

    static void SleepingBeauty(int sleepTimeInMs, int s)
    {
        var timeStamp = TimingMetricsEventSource.StartTiming();
        Thread.Sleep(sleepTimeInMs);
        TimingMetricsEventSource.StopTiming($"sleep{s}", timeStamp);
    }
}