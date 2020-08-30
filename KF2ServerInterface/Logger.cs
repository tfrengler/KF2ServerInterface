﻿using Serilog;
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
        public static ILogger Serilog;
        public static DirectoryInfo LogFolder = new DirectoryInfo(Environment.CurrentDirectory);
        public static string FileName { get; } = $"KF2ServerInterface_{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Day}.txt";

        public enum LogType
        {
            INFO,
            ERROR,
            WARNING
        };

        static Logger()
        {
            if (!LogFolder.Exists)
                throw new DirectoryNotFoundException("Unable to find log folder: " + LogFolder);

            Serilog = new LoggerConfiguration().WriteTo.Console().WriteTo.File($"{LogFolder.FullName}/{FileName}").CreateLogger();
        }

        public static void DumpHttpHeaders(HttpResponseMessage httpResponse)
        {
            StringBuilder output = new StringBuilder();
            output.Append("DUMP - HTTP HEADERS:" + Environment.NewLine);

            foreach (KeyValuePair<string, IEnumerable<string>> responseHeader in httpResponse.Headers)
            {
                output.Append($"{responseHeader.Key}: {string.Join(" ", responseHeader.Value)}" + Environment.NewLine);
            }
            foreach (KeyValuePair<string, IEnumerable<string>> contentHeader in httpResponse.Content.Headers)
            {
                output.Append($"{contentHeader.Key}: {string.Join(" ", contentHeader.Value)}" + Environment.NewLine);
            }

            Log(output.ToString(), LogType.WARNING);
        }

        public static void DumpResponseContent(HttpContent content)
        {
            Task<string> responseBody = content.ReadAsStringAsync();
            while (!responseBody.IsCompleted) ;

            string output = Regex.Replace(responseBody.Result, "<", "&lt;");
            output = Regex.Replace(output, ">", "&gt;");

            File.WriteAllTextAsync($"{LogFolder.FullName}/DEBUG_HttpResponseDump_{DateTime.Now.Year}-{DateTime.Now.Month}-{DateTime.Now.Day}.html", (output.Length > 0 ? $"<pre>{output}</pre>" : "<empty body>"));
        }

        public static void DumpCookies(CookieCollection cookies)
        {
            if (cookies.Count == 0)
            {
                Log("No cookies in collection to dump", LogType.ERROR);
                return;
            }

            StringBuilder output = new StringBuilder();
            output.Append("DUMP - CLIENT COOKIES" + Environment.NewLine);

            foreach (Cookie cookie in cookies)
                output.Append($"{cookie.Name}: {cookie.Value}" + Environment.NewLine);

            Log(output.ToString(), LogType.WARNING);
        }

        public static void Log(string output, LogType type = LogType.INFO)
        {
            if (type == LogType.INFO)
                Serilog.Information(output);
            else if (type == LogType.ERROR)
                Serilog.Error(output);
            else if (type == LogType.WARNING)
                Serilog.Warning(output);
        }
    }
}
