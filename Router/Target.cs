using System;
namespace MyLoadBalancer.Router
{
    public class Target
    {
        public required long Id { get; set; }
        public required Uri Url { get; set; }
        public required string HealthCheckPath { get; set; }
        public required int Weight { get; set; }
        public required int RequestsCount { get; set; }

        public Target()
        {
            RequestsCount = Constants.DefaulRequestsCount;
            Weight = Constants.DefaultWeight;
        }

        public bool MustGoToNext()
        {
            RequestsCount++;
            return RequestsCount > Weight;
        }
    }
}

