using System.Net;
using Zadatak34TriviaServer.Models;

namespace Zadatak34TriviaServer.Services
{
    public class HttpServer
    {
        private readonly string prefix;
        private readonly RequestQueue queue;
        private readonly Logger logger;
        private readonly ServerStats stats;

        private HttpListener? listener;
        private Thread? listenerThread;

        private volatile bool running = false;

        public HttpServer(
            string prefix,
            RequestQueue queue,
            Logger logger,
            ServerStats stats)
        {
            this.prefix = prefix;
            this.queue = queue;
            this.logger = logger;
            this.stats = stats;
        }

        public void Start()
        {
            listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();

            running = true;

            listenerThread = new Thread(ListenLoop);
            listenerThread.Name = "HTTP-Receiver";
            listenerThread.Start();

            logger.Info($"HTTP server slusa na {prefix}");
        }

        private void ListenLoop()
        {
            logger.Info("Nit za prijem HTTP zahteva je pokrenuta.");

            while (running)
            {
                HttpListenerContext? context = null;

                try
                {
                    // Prijem zahteva je odvojen od njegove obrade
                    context = listener!.GetContext();
                    stats.RequestReceived();

                    var request = new ClientRequest(context);

                    logger.Info(
                        $"Primljen zahtev {request.RequestId}: " +
                        $"{context.Request.HttpMethod} {context.Request.Url}");

                    if (!context.Request.HttpMethod.Equals(
                            "GET",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        SendSimpleError(
                            context,
                            405,
                            "Server podrzava samo GET metodu.");
                        continue;
                    }

                    if (!queue.Enqueue(request))
                    {
                        SendSimpleError(
                            context,
                            503,
                            "Server je trenutno preopterecen. Pokusajte kasnije.");

                        logger.Error(
                            $"Zahtev {request.RequestId} odbijen - red je pun.");
                    }
                    else
                    {
                        logger.Info(
                            $"Zahtev {request.RequestId} smesten u red. " +
                            $"Trenutna velicina reda: {queue.Count}");
                    }
                }
                catch (HttpListenerException)
                {
                    
                    if (running)
                    {
                        logger.Error("Greska prilikom prijema HTTP zahteva.");
                    }
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    logger.Error(
                        $"Neocekivana greska u prijemnoj niti: {ex.Message}");

                    if (context != null)
                    {
                        try
                        {
                            SendSimpleError(
                                context,
                                500,
                                "Greska prilikom prijema zahteva.");
                        }
                        catch
                        {
                        }
                    }
                }
            }

            logger.Info("Nit za prijem HTTP zahteva je zaustavljena.");
        }

        private static void SendSimpleError(
            HttpListenerContext context,
            int statusCode,
            string message)
        {
            string json =
                Newtonsoft.Json.JsonConvert.SerializeObject(
                    new
                    {
                        error = message,
                        statusCode = statusCode
                    });

            byte[] bytes =
                System.Text.Encoding.UTF8.GetBytes(json);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.ContentLength64 = bytes.Length;

            try
            {
                context.Response.OutputStream.Write(
                    bytes,
                    0,
                    bytes.Length);
            }
            finally
            {
                context.Response.OutputStream.Close();
            }
        }

        public void Stop()
        {
            if (!running)
            {
                return;
            }

            running = false;

            logger.Info("Zaustavljanje HTTP servera...");

            try
            {
                listener?.Stop();
                listener?.Close();
            }
            catch
            {
            }

            if (listenerThread != null &&
                listenerThread.IsAlive &&
                Thread.CurrentThread != listenerThread)
            {
                listenerThread.Join();
            }
        }
    }
}
