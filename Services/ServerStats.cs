namespace Zadatak34TriviaServer.Services
{
    public class ServerStats
    {
        private long totalRequests;
        private long successfulRequests;
        private long errorRequests;
        private long cacheHits;
        private long cacheMisses;

        public void RequestReceived()
        {
            Interlocked.Increment(ref totalRequests);
        }

        public void RequestSucceeded()
        {
            Interlocked.Increment(ref successfulRequests);
        }

        public void RequestFailed()
        {
            Interlocked.Increment(ref errorRequests);
        }

        public void CacheHit()
        {
            Interlocked.Increment(ref cacheHits);
        }

        public void CacheMiss()
        {
            Interlocked.Increment(ref cacheMisses);
        }

        public string GetReport(int queueCount)
        {
            return
                $"Zahtevi: {Interlocked.Read(ref totalRequests)}, " +
                $"uspesni: {Interlocked.Read(ref successfulRequests)}, " +
                $"greske: {Interlocked.Read(ref errorRequests)}, " +
                $"cache hit: {Interlocked.Read(ref cacheHits)}, " +
                $"cache miss: {Interlocked.Read(ref cacheMisses)}, " +
                $"red: {queueCount}";
        }
    }
}
