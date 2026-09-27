using Newtonsoft.Json.Linq;
using Zadatak34TriviaServer.Models;

namespace Zadatak34TriviaServer.Services
{
    public class TriviaService
    {
        private const string BaseUrl = "https://opentdb.com/api.php";

        private readonly HttpClient client;
        private readonly TriviaCache cache;
        private readonly Logger logger;
        private readonly ServerStats stats;

        public TriviaService(
            TriviaCache cache,
            Logger logger,
            ServerStats stats)
        {
            this.cache = cache;
            this.logger = logger;
            this.stats = stats;

            client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        public (string Body, int StatusCode) ProcessRequest(
            ClientRequest request)
        {
            string path = request.Context.Request.Url?.AbsolutePath ?? "";

            if (path.Equals("/status", StringComparison.OrdinalIgnoreCase))
            {
                return (CreateStatusResponse(), 200);
            }

            if (!path.Equals("/api.php", StringComparison.OrdinalIgnoreCase))
            {
                return (
                    CreateErrorResponse(
                        "Nepoznata putanja. Koristite /api.php?amount=23&type=boolean",
                        404),
                    404);
            }

            string? amountString =
                request.Context.Request.QueryString["amount"];

            string? type =
                request.Context.Request.QueryString["type"];

            if (!int.TryParse(amountString, out int amount) ||
                amount <= 0 ||
                amount > 50)
            {
                return (
                    CreateErrorResponse(
                        "Parametar amount mora biti ceo broj od 1 do 50.",
                        400),
                    400);
            }

            if (type != "boolean" && type != "multiple")
            {
                return (
                    CreateErrorResponse(
                        "Parametar type mora biti 'boolean' ili 'multiple'.",
                        400),
                    400);
            }

            string cacheKey = $"amount={amount}&type={type}";

            try
            {
                if (cache.TryGet(
                    cacheKey,
                    out string cachedValue,
                    out int cachedStatusCode))
                {
                    if (cachedStatusCode >= 200 &&
                        cachedStatusCode < 300)
                    {
                        stats.CacheHit();
                    }

                    return (cachedValue, cachedStatusCode);
                }

                stats.CacheMiss();

                string url =
                    $"{BaseUrl}?amount={amount}&type={Uri.EscapeDataString(type)}";

                logger.Info(
                    $"Request {request.RequestId}: slanje zahteva Open Trivia API-ju: {url}");

                // Blokirajuci poziv
                HttpResponseMessage response =
                    client.GetAsync(url).GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException(
                        $"Open Trivia API vratio HTTP status {(int)response.StatusCode}.");
                }

                string body =
                    response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                JObject json = JObject.Parse(body);

                int responseCode =
                    (int?)json["response_code"] ?? -1;

                if (responseCode != 0)
                {
                    string message = GetTriviaErrorMessage(responseCode);

                    logger.Error(
                        $"Request {request.RequestId}: {message}");

                    string errorBody =
                        CreateErrorResponse(message, 404);

                    // Cuvamo i negativan rezultat da ne poziva opet API
                    cache.Store(cacheKey, errorBody, 404);

                    return (errorBody, 404);
                }

                cache.Store(cacheKey, body, 200);

                logger.Info(
                    $"Request {request.RequestId}: rezultat uspesno pribavljen.");

                return (body, 200);
            }
            catch (Exception ex)
            {
                cache.StoreError(cacheKey, ex.Message);

                logger.Error(
                    $"Request {request.RequestId}: greska - {ex.Message}");

                return (
                    CreateErrorResponse(
                        "Greska prilikom pribavljanja pitanja: " + ex.Message,
                        500),
                    500);
            }
        }

        private string CreateStatusResponse()
        {
            return
                $"{{" +
                $"\"status\":\"ok\"," +
                $"\"cacheEntries\":{cache.Count}," +
                $"\"message\":\"Server radi ispravno.\"" +
                $"}}";
        }

        private static string CreateErrorResponse(
            string message,
            int statusCode)
        {
            string escaped =
                Newtonsoft.Json.JsonConvert.ToString(message);

            return
                $"{{" +
                $"\"error\":{escaped}," +
                $"\"statusCode\":{statusCode}" +
                $"}}";
        }

        private static string GetTriviaErrorMessage(int responseCode)
        {
            return responseCode switch
            {
                1 => "Nema dovoljno pitanja koja zadovoljavaju zadati kriterijum.",
                2 => "Prosledjeni parametri nisu validni.",
                3 => "Sesija pitanja nije pronadjena.",
                4 => "Sesija pitanja je iscrpljena.",
                5 => "Previse zahteva prema Open Trivia API-ju. Pokusajte kasnije.",
                _ => $"Open Trivia API je vratio nepoznat response_code: {responseCode}."
            };
        }
    }
}
