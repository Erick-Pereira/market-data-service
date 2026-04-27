using System;
using System.Collections.Generic;
using System.Text;

namespace Simcag.MarketDataService.Domain.Constants;

public static class EventNames
{
    /// <summary>Optional integration channel name (e.g. RabbitMQ routing key or Redis channel).</summary>
    public const string MarketDataUpdatedEvents = "market-data-updated-events";
}
