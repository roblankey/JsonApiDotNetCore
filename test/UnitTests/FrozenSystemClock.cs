using System;
using Microsoft.AspNetCore.Authentication;

namespace UnitTests
{
    internal class FrozenSystemClock : ISystemClock
    {
        public DateTimeOffset UtcNow { get; }

        public FrozenSystemClock()
            : this(new DateTimeOffset(new DateTime(2000, 1, 1)))
        {
        }

        public FrozenSystemClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }
    }
}
