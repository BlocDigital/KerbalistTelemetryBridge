using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ReduxLib.Logging;

namespace KerbalistTelemetry
{
    internal sealed class TelemetryServer
    {
        private readonly int _port;
        private readonly Func<string> _snapshot;
        private readonly ILogger _logger;
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;

        public TelemetryServer(int port, Func<string> snapshot, ILogger logger)
        {
            _port = port;
            _snapshot = snapshot;
            _logger = logger;
        }

        public void Start()
        {
            if (_running)
                return;

            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "KerbalistTelemetry.Accept" };
            _acceptThread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { if (_listener != null) _listener.Stop(); } catch { }
            _listener = null;
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
                }
                catch
                {
                    if (_running)
                        Thread.Sleep(50);
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                stream.ReadTimeout = 1000;
                string request = ReadHeaders(stream);
                if (string.IsNullOrEmpty(request))
                    return;

                string[] lines = request.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0)
                    return;

                string[] requestLine = lines[0].Split(' ');
                string method = requestLine.Length > 0 ? requestLine[0] : "GET";
                string path = requestLine.Length > 1 ? requestLine[1] : "/";

                var headers = ParseHeaders(lines);
                string wsKey;
                if (string.Equals(path, "/ws", StringComparison.OrdinalIgnoreCase) &&
                    headers.TryGetValue("Sec-WebSocket-Key", out wsKey))
                {
                    HandleWebSocket(stream, wsKey);
                    return;
                }

                if (string.Equals(method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                {
                    WriteHttp(stream, 204, "", "text/plain");
                    return;
                }

                if (string.Equals(path, "/api/status", StringComparison.OrdinalIgnoreCase))
                {
                    WriteHttp(stream, 200, "{\"ok\":true,\"name\":\"Kerbalist Telemetry Bridge\",\"schema\":\"kerbalist.telemetry.v2\",\"websocket\":\"ws://127.0.0.1:8765/ws\"}", "application/json; charset=utf-8");
                    return;
                }

                if (string.Equals(path, "/api/state", StringComparison.OrdinalIgnoreCase))
                {
                    WriteHttp(stream, 200, _snapshot(), "application/json; charset=utf-8");
                    return;
                }

                if (string.Equals(path, "/ut", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(path, "/api/ut", StringComparison.OrdinalIgnoreCase))
                {
                    string json = _snapshot();
                    double ut = ExtractUniversalTime(json);
                    WriteHttp(stream, 200, "{\"ut\":" + ut.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}", "application/json; charset=utf-8");
                    return;
                }

                if (string.Equals(path, "/api/bodies", StringComparison.OrdinalIgnoreCase))
                {
                    string json = _snapshot();
                    WriteHttp(stream, 200, json, "application/json; charset=utf-8");
                    return;
                }

                if (path == "/" || path == "/api")
                {
                    WriteHttp(stream, 200, "{\"name\":\"Kerbalist Telemetry Bridge\",\"schema\":\"kerbalist.telemetry.v2\",\"websocket\":\"ws://127.0.0.1:8765/ws\"}", "application/json; charset=utf-8");
                    return;
                }

                WriteHttp(stream, 404, "{\"error\":\"not found\"}", "application/json; charset=utf-8");
            }
        }

        private void HandleWebSocket(NetworkStream stream, string key)
        {
            string accept;
            using (var sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"));
                accept = Convert.ToBase64String(hash);
            }

            string response =
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                "Sec-WebSocket-Accept: " + accept + "\r\n" +
                "\r\n";
            byte[] handshake = Encoding.ASCII.GetBytes(response);
            stream.Write(handshake, 0, handshake.Length);

            stream.ReadTimeout = 10;
            while (_running)
            {
                try { SendWebSocketText(stream, _snapshot()); } catch { break; }
                try
                {
                    if (stream.DataAvailable) { var b = stream.ReadByte(); if (b < 0) break; }
                }
                catch { }
                Thread.Sleep(100);
            }
        }

        private static string ReadHeaders(NetworkStream stream)
        {
            var buffer = new byte[8192];
            var ms = new MemoryStream();
            while (ms.Length < 65536)
            {
                int read;
                try { read = stream.Read(buffer, 0, buffer.Length); }
                catch { break; }
                if (read <= 0)
                    break;
                ms.Write(buffer, 0, read);
                string current = Encoding.ASCII.GetString(ms.ToArray());
                if (current.IndexOf("\r\n\r\n", StringComparison.Ordinal) >= 0)
                    return current;
            }
            return Encoding.ASCII.GetString(ms.ToArray());
        }

        private static Dictionary<string, string> ParseHeaders(string[] lines)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0)
                    continue;
                result[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }
            return result;
        }

        private static void WriteHttp(NetworkStream stream, int status, string body, string contentType)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(body ?? "");
            string response =
                "HTTP/1.1 " + status + " " + StatusText(status) + "\r\n" +
                "Content-Type: " + contentType + "\r\n" +
                "Content-Length: " + bytes.Length + "\r\n" +
                "Access-Control-Allow-Origin: *\r\n" +
                "Access-Control-Allow-Methods: GET, OPTIONS\r\n" +
                "Access-Control-Allow-Headers: Content-Type\r\n" +
                "Connection: close\r\n\r\n";
            byte[] header = Encoding.ASCII.GetBytes(response);
            stream.Write(header, 0, header.Length);
            if (bytes.Length > 0)
                stream.Write(bytes, 0, bytes.Length);
        }

        private static double ExtractUniversalTime(string json)
        {
            if (string.IsNullOrEmpty(json))
                return 0;

            const string key = "\"universalTime\":";
            int start = json.IndexOf(key, StringComparison.Ordinal);
            if (start < 0)
                return 0;

            start += key.Length;
            int end = start;
            while (end < json.Length && "-+.0123456789eE".IndexOf(json[end]) >= 0)
                end++;

            double value;
            return double.TryParse(json.Substring(start, end - start), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static string StatusText(int status)
        {
            switch (status)
            {
                case 200: return "OK";
                case 204: return "No Content";
                case 404: return "Not Found";
                default: return "OK";
            }
        }

        private static void SendWebSocketText(NetworkStream stream, string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text ?? "");
            var frame = new List<byte>(payload.Length + 16);
            frame.Add(0x81);

            if (payload.Length < 126)
            {
                frame.Add((byte)payload.Length);
            }
            else if (payload.Length <= ushort.MaxValue)
            {
                frame.Add(126);
                frame.Add((byte)(payload.Length >> 8));
                frame.Add((byte)payload.Length);
            }
            else
            {
                frame.Add(127);
                ulong length = (ulong)payload.Length;
                for (int i = 7; i >= 0; i--)
                    frame.Add((byte)(length >> (i * 8)));
            }

            frame.AddRange(payload);
            byte[] bytes = frame.ToArray();
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
