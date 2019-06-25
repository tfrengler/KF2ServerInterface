using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KF2ServerInterface
{
    public static class Logger
    {
        public static string FileName { get; } = "log.html";

        public static void LogToFile(string output)
        {
            FileStream fileStream;
            StreamWriter outputFile;

            if (!File.Exists(FileName))
                fileStream = new FileStream(FileName, FileMode.CreateNew);
            else
                fileStream = new FileStream(FileName, FileMode.Append);

            outputFile = new StreamWriter(fileStream, Encoding.UTF8);
            outputFile.WriteLine(output + Environment.NewLine);

            outputFile.Dispose();
            fileStream.Dispose();
        }

        public static void DumpHttpHeaders(HttpResponseMessage httpResponse)
        {
            StringBuilder output = new StringBuilder();
            output.Append("------------HTTP HEADERS------------");

            foreach (KeyValuePair<string, IEnumerable<string>> responseHeader in httpResponse.Headers)
            {
                output.Append($"<p>{responseHeader.Key}: {string.Join(" ", responseHeader.Value)}</p>");
            }
            foreach (KeyValuePair<string, IEnumerable<string>> contentHeader in httpResponse.Content.Headers)
            {
                output.Append($"<p>{contentHeader.Key}: {string.Join(" ", contentHeader.Value)}</p>");
            }

            output.Append("------------END--------------------");
            LogToFile(output.ToString());
        }

        public static void DumpResponseContent(HttpContent content)
        {
            Task<string> responseBody = content.ReadAsStringAsync();
            while (!responseBody.IsCompleted) ;

            string output = Regex.Replace(responseBody.Result, "<", "&lt;");
            output = Regex.Replace(output, ">", "&gt;");

            LogToFile((output.Length > 0 ? $"<pre>{output}</pre>" : "<empty body>"));
        }
    }
}
