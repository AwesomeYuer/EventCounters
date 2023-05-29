using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Metrics;

[EventSource(Name = "Simple-Timing-Metrics-EventSource")]
public sealed class TimingMetricsEventSource : EventSource
{
    private static IDictionary<string, EventCounter> _dynamicEventCounters =
                        new Dictionary<string, EventCounter>(StringComparer.OrdinalIgnoreCase);

   //public static TimingMetricsEventSource Log = new TimingMetricsEventSource();

    [NonEvent]
    public static void AddEventCounters(params EventCounter[] eventCounters)
    { 
        foreach (var counter in eventCounters) 
        {
            _ = _dynamicEventCounters.TryAdd(counter.Name, counter);
        }
    }

    [NonEvent]
    public static void AddEventCounters(params string[] eventCountersNames)
    {
        //foreach (var counterName in eventCountersNames)
        //{
        //    _ = _dynamicEventCounters
        //                .TryAdd
        //                        (
        //                            counterName
        //                            , new EventCounter(counterName, this)
        //                            { 
                                    
                                    
        //                            }
        //                        );
        //}
    }


    [NonEvent]
    public static void Timing(string eventCounterName, Action action)
    {
        var start = Stopwatch.GetTimestamp();
        action();
        StopTiming(eventCounterName, start);
    }

    [NonEvent]
    public static long StartTiming()
    {
        return Stopwatch.GetTimestamp();
    }

    [NonEvent]
    public static void StopTiming(string eventCounterName, long startTimestamp)
    {
        var end = Stopwatch.GetTimestamp();
        var duration = new TimeSpan(end - startTimestamp).TotalMilliseconds;
        EventCounter eventCounter = null!;
        if (_dynamicEventCounters.TryGetValue(eventCounterName, out eventCounter!))
        {
            eventCounter.WriteMetric(duration);
        }
    }
}