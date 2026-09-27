using System.Net;
using Zadatak34TriviaServer.Services;

namespace Zadatak34TriviaServer
{
    public class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("   Open Trivia Database - Web Server");
            Console.WriteLine("   Zadatak 34 - Sistemsko programiranje");
            Console.WriteLine("==============================================\n");

            string prefix = "http://localhost:8080/";
            int workerCount = 4;

            Console.WriteLine($"Adresa servera: {prefix}");
            Console.WriteLine($"Broj worker niti: {workerCount}");
            Console.WriteLine("Strategija kesiranja: vremensko isticanje");
            Console.WriteLine("Zaustavljanje servera: Ctrl+C\n");

            var requestQueue = new RequestQueue(1000);
            var logger = new Logger("server.log");
            var stats = new ServerStats();

            var cache = new TriviaCache(
                expiration: TimeSpan.FromMinutes(2),
                logger: logger);

            var triviaService = new TriviaService(
                cache: cache,
                logger: logger,
                stats: stats);

            var workerPool = new WorkerPool(
                queue: requestQueue,
                workerCount: workerCount,
                triviaService: triviaService,
                logger: logger,
                stats: stats);

            var server = new HttpServer(
                prefix: prefix,
                queue: requestQueue,
                logger: logger,
                stats: stats);

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                server.Stop();
            };

            try
            {
                workerPool.Start();
                server.Start();

                Console.WriteLine("Server je pokrenut.");
                Console.WriteLine("Primer:");
                Console.WriteLine("http://localhost:8080/api.php?amount=23&type=boolean");
                Console.WriteLine();
                Console.WriteLine("Pritisnite Enter za zaustavljanje servera.");

                Console.ReadLine();
            }
            catch (HttpListenerException ex)
            {
                Console.WriteLine($"Greska prilikom pokretanja HTTP servera: {ex.Message}");
                Console.WriteLine("Ako Windows prijavljuje problem sa URL rezervacijom, pokrenite Visual Studio kao Administrator.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Greska: {ex.Message}");
            }
            finally
            {
                server.Stop();
                workerPool.Stop();
                logger.Dispose();
            }
        }
    }
}
