// Nested namespace types for recursive wildcard testing

namespace ConcordIO.AsyncApi.Tests.TestTypes.Nested
{
    public class RootLevelEvent
    {
        public string Data { get; set; } = string.Empty;
    }
}

namespace ConcordIO.AsyncApi.Tests.TestTypes.Nested.Level1
{
    public class Level1Event
    {
        public string Data { get; set; } = string.Empty;
    }
}

namespace ConcordIO.AsyncApi.Tests.TestTypes.Nested.Level1.Level2
{
    public class Level2Event
    {
        public string Data { get; set; } = string.Empty;
    }
}
