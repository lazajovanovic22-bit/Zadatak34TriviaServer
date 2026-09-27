namespace Zadatak34TriviaServer.Services
{
    public class Logger : IDisposable
    {
        private readonly object logLock = new object();
        private readonly StreamWriter writer;

        public Logger(string filePath)
        {
            writer = new StreamWriter(filePath, append: true)
            {
                AutoFlush = true
            };
        }

        public void Info(string message)
        {
            Write("INFO", message);
        }

        public void Error(string message)
        {
            Write("ERROR", message);
        }

        private void Write(string level, string message)
        {
            lock (logLock)
            {
                string line =
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] " +
                    $"[Thread {Environment.CurrentManagedThreadId}] " +
                    $"[{level}] {message}";

                Console.WriteLine(line);
                writer.WriteLine(line);
            }
        }

        public void Dispose()
        {
            lock (logLock)
            {
                writer.Dispose();
            }
        }
    }
}
