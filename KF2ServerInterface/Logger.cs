using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace KF2ServerInterface
{
    public static class Logger
    {
        public static string FileName { get; } = "log.html";
        public static bool OutputToFile { get; set; } = true;
        public static bool OutputInfoToFile { get; set; } = false;
        public static bool OutputErrorsToFile { get; set; } = true;
        public static bool OutputDebugToFile { get; set; } = false;
        public static int MaxFileSize { get; } = 10 * 1024 * 1024;

        public enum LogType
        {
            INFO,
            ERROR,
            DEBUG
        };

        public static void LogToFile(string output)
        {
            FileStream fileStream;
            StreamWriter outputFile;

            if (!File.Exists(FileName))
                fileStream = new FileStream(FileName, FileMode.CreateNew);
            else
                fileStream = new FileStream(FileName, FileMode.Append);

            outputFile = new StreamWriter(fileStream, Encoding.UTF8);
            outputFile.WriteLine(DateTime.Now.ToString("<p>------------------[dd/MM/yyyy - HH:mm:ss]:---------------------</p>"));
            outputFile.WriteLine($"<p>{output}</p>");
            outputFile.WriteLine($"<p>---------------------------------------------------------------<p>");

            outputFile.Dispose();
            fileStream.Dispose();
        }

        public static void DumpHttpHeaders(HttpResponseMessage httpResponse)
        {
            StringBuilder output = new StringBuilder();
            output.Append("<p>HTTP HEADERS:</p>");

            foreach (KeyValuePair<string, IEnumerable<string>> responseHeader in httpResponse.Headers)
            {
                output.Append($"<div>{responseHeader.Key}: {string.Join(" ", responseHeader.Value)}</div>");
            }
            foreach (KeyValuePair<string, IEnumerable<string>> contentHeader in httpResponse.Content.Headers)
            {
                output.Append($"<div>{contentHeader.Key}: {string.Join(" ", contentHeader.Value)}</div>");
            }

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

        public static void DumpCookies(CookieCollection cookies)
        {
            if (cookies.Count == 0)
            {
                LogToFile("No cookies in collection to dump");
                return;
            }

            StringBuilder output = new StringBuilder();
            output.Append("------------CLIENT COOKIES------------<br/>");

            foreach (Cookie cookie in cookies)
                output.Append($"<div>{cookie.Name}: {cookie.Value}<div>");

            LogToFile(output.ToString());
        }

        public static void Log(string output, LogType type)
        {
            if (type == LogType.INFO)
                Console.WriteLine(output);

            if (!OutputToFile)
                return;

            if (type == LogType.INFO && OutputInfoToFile)
                LogToFile(output);
            else if (type == LogType.ERROR && OutputErrorsToFile)
                LogToFile(output);
            else if (type == LogType.DEBUG && OutputDebugToFile)
                LogToFile(output);
        }
    }
}
