using DnstapLogger;
using DnstapLogger.Protocol;
using Microsoft.Extensions.Logging;
using ARSoft.Tools.Net.Dns;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Client
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var options = ParseArgs(args);

            if (!options.TryGetValue("target", out var target) || (target != "file" && target != "tcp"))
            {
                Console.Error.WriteLine("Usage: --target=file|tcp [--file=path] [--host=host --port=port] [--identity=id] [--version=v]");
                return 1;
            }

            var identity = options.TryGetValue("identity", out var id) ? id : null;
            var version = options.TryGetValue("version", out var ver) ? ver : null;

            DnstapWriter writer;

            if (target == "file")
            {
                if (!options.TryGetValue("file", out var filePath))
                {
                    Console.Error.WriteLine("Missing --file argument");
                    return 1;
                }

                writer = DnstapWriter.CreateFileWriter(filePath);
            }
            else
            {
                if (!options.TryGetValue("host", out var host) ||
                    !options.TryGetValue("port", out var portStr) ||
                    !int.TryParse(portStr, out var port))
                {
                    Console.Error.WriteLine("Missing or invalid host/port");
                    return 1;
                }

                writer = await DnstapWriter.ConnectTcpAsync(host, port);
            }

            using (writer)
            {
                await writer.StartAsync();

                for (var i = 1; i <= 100; i++)
                {
                    // ------------------------------------------------------------------
                    // CREATE A DNSTAP MESSAGE WITH WIRE-FORMAT QUERY
                    // ------------------------------------------------------------------
                    var msg = new DnstapMessage(
                        queryMessage: "example.com",              // <--- REAL DNS WIREFORMAT
                        queryType: MessageType.ToolQuery,   // TOOL_QUERY is fine, spec-compliant
                        queryAddress: IPAddress.Parse("192.0.2.100"),
                        queryPort: 54321,
                        queryZone: "example.com"            // wrapper converts to DNS NAME correctly
                    );

                    await writer.WriteMessageAsync(msg);

                    await Task.Delay(100); // simulate async workload
                }

                await writer.StopAsync();
                await Task.Delay(200); // allow socket flush
            }

            return 0;
        }

        private static Dictionary<string, string> ParseArgs(string[] args)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var arg in args)
            {
                if (arg.StartsWith("--"))
                {
                    var parts = arg.Substring(2).Split('=', 2);
                    var key = parts[0];
                    var val = (parts.Length > 1) ? parts[1] : "true";
                    dict[key] = val;
                }
            }

            return dict;
        }
    }
}
