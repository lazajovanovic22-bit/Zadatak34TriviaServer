using System.Net;

namespace Zadatak34TriviaServer.Models
{
    public class ClientRequest
    {
        public HttpListenerContext Context { get; }
        public string RequestId { get; }
        public DateTime ReceivedAt { get; }

        public ClientRequest(HttpListenerContext context)
        {
            Context = context;
            RequestId = Guid.NewGuid().ToString("N")[..8];
            ReceivedAt = DateTime.Now;
        }
    }
}
