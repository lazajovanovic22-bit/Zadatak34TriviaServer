namespace Zadatak34TriviaServer.Services
{
    public class TriviaCache
    {
        private class CacheEntry
        {
            public string Value { get; }
            public int StatusCode { get; }
            public DateTime ExpirationTime { get; }

            public CacheEntry(
                string value,
                int statusCode,
                DateTime expirationTime)
            {
                Value = value;
                StatusCode = statusCode;
                ExpirationTime = expirationTime;
            }
        }

        private readonly Dictionary<string, CacheEntry> cache =
            new Dictionary<string, CacheEntry>();

        private readonly HashSet<string> loadingKeys =
            new HashSet<string>();

        private readonly object cacheLock = new object();

        private readonly TimeSpan expiration;
        private readonly Logger logger;

        public TriviaCache(TimeSpan expiration, Logger logger)
        {
            this.expiration = expiration;
            this.logger = logger;
        }

        
        public bool TryGet(
            string key,
            out string value,
            out int statusCode)
        {
            lock (cacheLock)
            {
                while (true)
                {
                    if (cache.TryGetValue(key, out CacheEntry? entry))
                    {
                        if (entry.ExpirationTime > DateTime.Now)
                        {
                            value = entry.Value;
                            statusCode = entry.StatusCode;

                            logger.Info(
                                $"CACHE HIT: '{key}' (HTTP {statusCode})");

                            return true;
                        }

                        cache.Remove(key);
                        logger.Info($"CACHE EXPIRED: '{key}'");
                    }

                    if (!loadingKeys.Contains(key))
                    {
                        // Ova nit postaje vlasnik pribavljanja podataka
                        loadingKeys.Add(key);

                        value = string.Empty;
                        statusCode = 0;

                        logger.Info(
                            $"CACHE MISS: '{key}' - nit dobija pravo pribavljanja.");

                        return false;
                    }

                    // Druga nit trenutno pribavlja isti resurs
                    logger.Info(
                        $"CACHE WAIT: '{key}' - cekanje na rezultat druge niti.");

                    Monitor.Wait(cacheLock);
                }
            }
        }

        public void Store(
            string key,
            string value,
            int statusCode)
        {
            lock (cacheLock)
            {
                DateTime expirationTime = DateTime.Now.Add(expiration);

                cache[key] = new CacheEntry(
                    value,
                    statusCode,
                    expirationTime);

                loadingKeys.Remove(key);

                logger.Info(
                    $"CACHE STORE: '{key}', HTTP {statusCode}, " +
                    $"istice u {expirationTime:HH:mm:ss}");

                
                Monitor.PulseAll(cacheLock);
            }
        }

        public void StoreError(
            string key,
            string errorBody)
        {
            
            lock (cacheLock)
            {
                loadingKeys.Remove(key);
                Monitor.PulseAll(cacheLock);
            }
        }

        public int Count
        {
            get
            {
                lock (cacheLock)
                {
                    var expiredKeys = cache
                        .Where(x => x.Value.ExpirationTime <= DateTime.Now)
                        .Select(x => x.Key)
                        .ToList();

                    foreach (var key in expiredKeys)
                    {
                        cache.Remove(key);
                    }

                    return cache.Count;
                }
            }
        }
    }
}
