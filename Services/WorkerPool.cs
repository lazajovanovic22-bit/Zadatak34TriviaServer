using Zadatak34TriviaServer.Models;

namespace Zadatak34TriviaServer.Services
{
    public class WorkerPool
    {
        private readonly RequestQueue queue;
        private readonly TriviaService triviaService;
        private readonly Logger logger;
        private readonly ServerStats stats;

        private readonly int workerCount;
        private readonly List<Thread> workers = new List<Thread>();

        private bool stopped = false;

        public WorkerPool(
            RequestQueue queue,
            int workerCount,
            TriviaService triviaService,
            Logger logger,
            ServerStats stats)
        {
            this.queue = queue;
            this.workerCount = workerCount;
            this.triviaService = triviaService;
            this.logger = logger;
            this.stats = stats;
        }

        public void Start()
        {
            for (int i = 0; i < workerCount; i++)
            {
                int workerNumber = i + 1;

                Thread worker = new Thread(() =>
                {
                    WorkerLoop(workerNumber);
                });

                worker.Name = $"Worker-{workerNumber}";
                workers.Add(worker);
                worker.Start();
            }
        }

        private void WorkerLoop(int workerNumber)
        {
            logger.Info($"Worker {workerNumber} je pokrenut.");

            while (true)
            {
                ClientRequest? request = queue.Dequeue();

                if (request == null)
                {
                    break;
                }

                logger.Info(
                    $"Worker {workerNumber} obradjuje zahtev {request.RequestId}.");

                try
                {
                    var result =
                        triviaService.ProcessRequest(request);

                    WriteResponse(
                        request,
                        result.Body,
                        result.StatusCode);

                    if (result.StatusCode >= 200 &&
                        result.StatusCode < 300)
                    {
                        stats.RequestSucceeded();
                    }
                    else
                    {
                        stats.RequestFailed();
                    }
                }
                catch (Exception ex)
                {
                    stats.RequestFailed();

                    logger.Error(
                        $"Worker {workerNumber}, zahtev {request.RequestId}: {ex.Message}");

                    try
                    {
                        WriteResponse(
                            request,
                            $"{{\"error\":\"Interna greska servera: {EscapeJson(ex.Message)}\"}}",
                            500);
                    }
                    catch
                    {
                        
                    }
                }
            }

            logger.Info($"Worker {workerNumber} je zaustavljen.");
        }

        private void WriteResponse(
            ClientRequest request,
            string body,
            int statusCode = 200)
        {
            byte[] bytes =
                System.Text.Encoding.UTF8.GetBytes(body);

            var response = request.Context.Response;

            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;

            try
            {
                response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }

        private static string EscapeJson(string text)
        {
            return Newtonsoft.Json.JsonConvert.ToString(text);
        }

        public void Stop()
        {
            if (stopped)
            {
                return;
            }

            stopped = true;

            queue.Stop();

            foreach (Thread worker in workers)
            {
                if (worker.IsAlive)
                {
                    worker.Join();
                }
            }
        }
    }
}
