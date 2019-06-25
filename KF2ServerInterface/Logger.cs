using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace KF2ServerInterface
{
    public static class Logger
    {
        public static string FileName { get; } = "log.html";

        public static void LogToFile(string fileName, string[] output)
        {

        }

        public static void DumpContentHeaders(HttpContentHeaders headers)
        {
            Console.WriteLine($"CONTENT HEADER DUMP ({headers.Count()}):--------------------------------");
            foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
            {
                Console.WriteLine($"{header.Key}: {string.Join(" ", header.Value)}");
            }
            Console.WriteLine("END DUMP:--------------------------------");
        }

        public static void DumpResponseHeaders(HttpHeaders headers)
        {
            Console.WriteLine($"RESPONSE HEADER DUMP ({headers.Count()}):--------------------------------");
            foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
            {
                Console.WriteLine($"{header.Key}: {string.Join(" ", header.Value)}");
            }
            Console.WriteLine("END DUMP:--------------------------------");
        }

        public static void DumpResponseContent(HttpContent content)
        {
            Console.WriteLine("CONTENT DUMP:--------------------------------");

            Task<string> responseBody = content.ReadAsStringAsync();
            while (!responseBody.IsCompleted) ;
            Console.WriteLine(responseBody.Result);

            Console.WriteLine("END DUMP:--------------------------------");
        }
    }
}
