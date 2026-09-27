using Zadatak34TriviaServer.Models;

namespace Zadatak34TriviaServer.Services
{
    public class RequestQueue
    {
        private readonly Queue<ClientRequest> queue = new Queue<ClientRequest>();
        private readonly object queueLock = new object();

        private readonly int maxSize;
        private bool stopped = false;

        public RequestQueue(int maxSize)
        {
            this.maxSize = maxSize;
        }

        
        // Nema aktivnog cekanja (busy waiting) - nit ceka preko Monitor.Wait()
        public bool Enqueue(ClientRequest request)
        {
            lock (queueLock)
            {
                if (stopped || queue.Count >= maxSize)
                {
                    return false;
                }

                queue.Enqueue(request);

                // Budi jednu worker nit koja ceka na nov zahtev
                Monitor.Pulse(queueLock);

                return true;
            }
        }

        public ClientRequest? Dequeue()
        {
            lock (queueLock)
            {
                // Worker ceka dok je red prazan ili dok server nije zaustavljen.
                while (queue.Count == 0 && !stopped)
                {
                    Monitor.Wait(queueLock);
                }

                if (queue.Count == 0 && stopped)
                {
                    return null;
                }

                return queue.Dequeue();
            }
        }

        public void Stop()
        {
            lock (queueLock)
            {
                stopped = true;

                
                Monitor.PulseAll(queueLock);
            }
        }

        public int Count
        {
            get
            {
                lock (queueLock)
                {
                    return queue.Count;
                }
            }
        }
    }
}
