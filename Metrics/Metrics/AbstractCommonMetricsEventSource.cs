using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace Metrics;
public abstract class AbstractCommonMetricsEventSource : EventSource
{
    private class CounterContainer
    {
        public DiagnosticCounter? Counter;
        public long Count;
    }

    private Dictionary
                    <
                        string
                        ,
                        (
                              EventCounter DurationCounter
                            , CounterContainer ProcessCounter
                            , CounterContainer ProcessingCounter
                            , CounterContainer ProcessedCounter
                            , CounterContainer ProcessRateCounter
                            , CounterContainer ProcessedRateCounter
                        )
                    > _dynamicEventCounters = new (StringComparer.InvariantCultureIgnoreCase);
                        

    //public static readonly TimingMetricsEventSource Logger = new ();

    private object _locker = new object ();
    
    [NonEvent]
    public bool AddCounters(string countersNamePrefix)
    {
        var counterName = $"{countersNamePrefix}-duration";
        var eventCounter = new EventCounter(counterName, this)
        {
              DisplayName = counterName
            , DisplayUnits = "ms/op"
        };
        counterName = $"{countersNamePrefix}-Process";
        var processCounter = new PollingCounter
                                    (
                                        counterName
                                        , this
                                        , () =>
                                        {
                                            return
                                                _dynamicEventCounters
                                                    [countersNamePrefix]
                                                                .ProcessCounter
                                                                .Count;
                                        }
                                    )
        { 
              DisplayName = counterName
            , DisplayUnits = "op"
        };

        counterName = $"{countersNamePrefix}-Processing";
        var processingCounter = new PollingCounter
                                    (
                                        counterName
                                        , this
                                        , () =>
                                        {
                                            return
                                                _dynamicEventCounters
                                                    [countersNamePrefix]
                                                                .ProcessingCounter
                                                                .Count;
                                        }
                                    )
        {
              DisplayName = counterName
            , DisplayUnits = "op"
        };

        counterName = $"{countersNamePrefix}-Processed";
        var processedCounter = new PollingCounter
                                    (
                                        counterName
                                        , this
                                        , () =>
                                        {
                                            return
                                                _dynamicEventCounters
                                                    [countersNamePrefix]
                                                                .ProcessedCounter
                                                                .Count;
                                        }
                                    )
        {
              DisplayName = counterName
            , DisplayUnits = "op"
        };

        counterName = $"{countersNamePrefix}-Process-Rate";
        var processRateIncrementingPollingCounter =
                new IncrementingPollingCounter
                        (
                            counterName
                            , this
                            , () =>
                            {
                                return
                                    _dynamicEventCounters
                                            [countersNamePrefix]
                                                    .ProcessRateCounter
                                                    .Count;
                            }
                        )
                {
                    DisplayName = counterName
                    , DisplayRateTimeScale = TimeSpan.FromSeconds(1)
                    , DisplayUnits = "ops/sec"

                };

        counterName = $"{countersNamePrefix}-Processed-Rate";
        var processedRateIncrementingPollingCounter =
                new IncrementingPollingCounter
                        (
                            counterName
                            , this
                            , () =>
                            {
                                return
                                    _dynamicEventCounters
                                            [countersNamePrefix]
                                                    .ProcessedRateCounter
                                                    .Count;
                            }
                        )
                {
                      DisplayName = counterName
                    , DisplayRateTimeScale = TimeSpan.FromSeconds( 1 )
                    , DisplayUnits = "ops/sec"

                };


        var r = _dynamicEventCounters
                                .TryAdd
                                        (
                                            countersNamePrefix
                                            ,
                                            (
                                                  eventCounter
                                                , new CounterContainer()
                                                {
                                                       Counter =  processCounter
                                                     , Count = 0
                                                }
                                                , new CounterContainer()
                                                {
                                                      Counter = processingCounter
                                                    , Count = 0
                                                }
                                                , new CounterContainer()
                                                {
                                                      Counter = processedCounter
                                                    , Count = 0
                                                }
                                                , new CounterContainer()
                                                { 
                                                      Counter = processRateIncrementingPollingCounter
                                                    , Count = 0
                                                }
                                                , new CounterContainer()
                                                {
                                                      Counter = processRateIncrementingPollingCounter
                                                    , Count = 0
                                                }
                                            )
                                        );
        return r;
    }

    [NonEvent]
    public void Timing(string eventCounterName, Action action)
    {
        if (IsEnabled())
        {
            var start = Stopwatch.GetTimestamp();
            action();
            StopCounting(eventCounterName, start);
        }
    }

    [NonEvent]
    public long StartCounting(string countersNamePrefix)
    {
        if (IsEnabled())
        {
            Console.WriteLine("enabled");
            var startTimestamp = Stopwatch.GetTimestamp();
            if (!_dynamicEventCounters.TryGetValue(countersNamePrefix, out var counters))
            {
                lock (_locker)
                {
                    AddCounters(countersNamePrefix);
                }
                counters = _dynamicEventCounters[countersNamePrefix];
            }
            Interlocked.Increment(ref counters.ProcessCounter.Count);
            Interlocked.Increment(ref counters.ProcessingCounter.Count);
            
            Interlocked.Increment(ref counters.ProcessRateCounter.Count);
            return startTimestamp;
        }
        Console.WriteLine("not enabled");
        return 0;
    }

    [NonEvent]
    public void StopCounting(string countersNamePrefix, long startTimestamp)
    {
        if (IsEnabled())
        {
            var endTimestamp = Stopwatch.GetTimestamp();
            var metric = new TimeSpan(endTimestamp - startTimestamp).TotalMilliseconds;

            if (!_dynamicEventCounters.TryGetValue(countersNamePrefix, out var counters))
            {
                lock (_locker)
                {
                    AddCounters(countersNamePrefix);
                }
                counters = _dynamicEventCounters[countersNamePrefix];
            }

            counters.DurationCounter.WriteMetric(metric);
            Interlocked.Increment(ref counters.ProcessedCounter.Count);
            var l = Interlocked.Decrement(ref counters.ProcessingCounter.Count);
            if (l < 0)
            {
                Interlocked.Exchange(ref counters.ProcessingCounter.Count, 0);
            }
            Interlocked.Increment(ref counters.ProcessedRateCounter.Count);
        }
    }
}