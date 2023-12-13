using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Logging;

namespace DrumGame.Game.API;

// https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
public class DrumWebSocket : IDisposable
{
    public const string DrumGameWebUrl = "https://jumprocks1.github.io/drum-game";
    public const int Port = 412;
    const int MaxMessageLength = 1 << 16; // 64 KB
    const string KeyHeader = "Sec-WebSocket-Key: ";
    protected static object _lock = new();
    static Stopwatch Stopwatch;

    public bool Connected => clientReady;
    public DrumWebSocket()
    {
        lock (_lock) Stopwatch ??= Stopwatch.StartNew();
        Task.Factory.StartNew(StartServer, TaskCreationOptions.LongRunning);
    }
    TcpListener server;
    TcpClient client;
    NetworkStream stream;
    bool clientReady;

    void Listen()
    {
        TcpClient clientRef = null;
        try
        {
            using var localClient = server.AcceptTcpClient();
            clientRef = localClient;
            lock (_lock)
            {
                if (client != null)
                {
                    client.Dispose();
                    client = null;
                    stream.Dispose();
                    stream = null;
                    clientReady = false;
                }
                client = localClient;
                Task.Factory.StartNew(Listen, TaskCreationOptions.LongRunning); // listen for more clients
            }

            Logger.Log($"Client connected to WebSocket server", level: LogLevel.Important);

            using var localStream = client.GetStream();
            stream = localStream;

            var buffer = new byte[4096];
            var offset = 0;
            while (true)
            {
                if (buffer.Length < offset + 512)
                {
                    if (buffer.Length * 2 > MaxMessageLength) throw new NotSupportedException();
                    var oldBuffer = buffer;
                    buffer = new byte[buffer.Length * 2];
                    Array.Copy(oldBuffer, buffer, offset);
                }
                offset += localStream.Read(buffer, offset, buffer.Length - offset);
                if (localStream.DataAvailable) continue;
                // Console.WriteLine($"parsing {offset} bytes");
                if (ParseMessage(localStream, buffer, offset)) // true on first connection message
                {
                    clientReady = true;
                    AfterConnection(localClient);
                }
                offset = 0;
            }
        }
        catch (IOException e) when (e.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionAborted)
        {
            Logger.Log($"Client disconnected");
        }
        catch (SocketException e) when (e.SocketErrorCode == SocketError.Interrupted)
        {
            // means we canceled the listener by disposing calling server.Stop()
        }
        lock (_lock)
        {
            if (client == clientRef)
            {
                client = null;
                stream = null;
                clientReady = false;
            }
        }
    }

    protected virtual void AfterConnection(TcpClient client) { }

    void StartServer()
    {
        try
        {
            server = new TcpListener(IPAddress.Parse("127.0.0.1"), Port);
            server.Start();
            Logger.Log($"WebSocket server started on :{Port}", level: LogLevel.Important);
            Listen();
        }
        catch (Exception e)
        {
            Logger.Error(e, "WebSocket error");
        }
    }

    protected virtual void HandleMessage(byte[] message) => throw new NotSupportedException();

    bool ParseMessage(NetworkStream stream, byte[] buffer, int length)
    {
        // Console.WriteLine(string.Join(',', buffer[..length]));
        var s = Encoding.UTF8.GetString(buffer, 0, length);

        if (buffer[0] == 'G') // the first byte of a websocket message should never be even close to 'G'
        {
            // see https://developer.mozilla.org/en-US/docs/Web/API/WebSockets_API/Writing_WebSocket_server
            var keyI = s.IndexOf(KeyHeader) + KeyHeader.Length;
            var keyEnd = s.IndexOf('\n', keyI);
            var swk = s.Substring(keyI, keyEnd - keyI).Trim();
            string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            string swkaSha1 = Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(swka)));

            // HTTP/1.1 defines the sequence CR LF as the end-of-line marker
            byte[] response = Encoding.UTF8.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Connection: Upgrade\r\n" +
                "Upgrade: websocket\r\n" +
                "Sec-WebSocket-Accept: " + swkaSha1 + "\r\n\r\n");

            stream.Write(response, 0, response.Length);
            return true;
        }
        else
        {
            var fin = (buffer[0] & 0b10000000) != 0;
            var mask = (buffer[1] & 0b10000000) != 0;
            if (!mask) throw new NotSupportedException("Mask bit not set");

            var opcode = buffer[0] & 0b00001111; // expecting 1 - text message or 2 - binary
            var msglen = buffer[1] & 0b01111111;

            var offset = 2;

            if (msglen == 126)
            {
                // was ToUInt16(bytes, offset) but the result is incorrect
                msglen = BitConverter.ToUInt16(new byte[] { buffer[3], buffer[2] }, 0);
                offset += 2;
            }
            else if (msglen == 127)
            {
                var longLength = BitConverter.ToUInt64(new byte[] {
                     buffer[9], buffer[8], buffer[7], buffer[6], buffer[5], buffer[4], buffer[3], buffer[2] }, 0);
                if (longLength > int.MaxValue) throw new NotSupportedException();
                msglen = (int)longLength;
                offset += 8;
            }

            var decoded = new byte[msglen];
            var masks = new byte[4] { buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3] };
            offset += 4;

            for (int i = 0; i < msglen; ++i)
                decoded[i] = (byte)(buffer[offset + i] ^ masks[i % 4]);

            HandleMessage(decoded);
        }
        return false;
    }

    public void Dispose()
    {
        server?.Stop();
    }


    public void SendJson(object json) => SendMessage(stream, JsonConvert.SerializeObject(json));
    static void SendMessage(NetworkStream stream, byte[] message)
    {
        if (message.Length > 126) throw new NotImplementedException();
        var output = new byte[message.Length + 2];
        output[0] = 0b1000_0001;
        output[1] = (byte)(0b0000_0000 | message.Length);
        Buffer.BlockCopy(message, 0, output, 2, message.Length);
        stream.Write(output);
    }
    static void SendMessage(NetworkStream stream, string message) => SendMessage(stream, Encoding.UTF8.GetBytes(message));
}