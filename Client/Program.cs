using DnstapLogger;
using Microsoft.Extensions.Logging;
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

                // file writers require no handshake
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

            // We can use direct writes or a logging provider
            using (writer)
            //using (var provider = new DnstapLoggerProvider(writer, identity, version, LogLevel.Information))
            {
                await writer.StartAsync();
                //var logger = provider.CreateLogger("Client");

                for (var i = 1; i <= 100; i++)
                {
                    var msg = new DnstapMessage(
                        queryMessage: $"\u0000\u0000 wire-format mock query #{i}",
                        queryAddress: IPAddress.Parse("192.0.2.100"),
                        queryPort: 54321,
                        queryZone: "example.com",
                        responseAddress: IPAddress.Parse("192.0.2.1"),
                        responsePort: 53);
                    await writer.WriteMessageAsync(msg);

                    //logger.LogInformation(msg.ToString());
                    await Task.Delay(100); // simulate some delay between messages
                }

                // send STOP and flush
                await writer.StopAsync();

                // give OS time to flush TCP buffers before exiting
                await Task.Delay(200);
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