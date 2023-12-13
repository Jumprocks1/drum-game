using System;
using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using DrumGame.Game.Midi;

namespace DrumGame.Game.API;

// Handle Roland eclusive MIDI messaging that can be used to view/configure the TD-27 module
public class RolandConfigurator : IDisposable
{
    // there are 3 messages we can send
    //    identity request (0x7E)
    //    data request (0x41)
    //    data set (0x41)
    // these 3 messages all start with 0xF0 and end with 0xF7

    // there are 2 messages we can receive
    //    identity reply
    //    data set

    byte[] memoryDump;
    public RolandConfigurator()
    {
        DrumMidiHandler.ExtraMidiHandler += OnMidiEvent;
    }

    void OnMidiEvent(byte[] bytes)
    {
        DebugBytes(bytes, "Receiving");
    }

    public static void DebugBytes(Span<byte> span, string message = null)
    {
        var hex = new StringBuilder(span.Length * 3);
        if (message != null)
        {
            hex.Append(message);
            hex.Append(": ");
        }
        foreach (var b in span)
            hex.AppendFormat("{0:x2} ", b);
        Console.WriteLine(hex);
    }

    public uint ModelId;
    public byte ManufacturerId;

    byte DeviceId;

    public bool? RolandVerified => ManufacturerId == 0 ? null : ManufacturerId == 0x41;
    public async Task<bool> CheckRolandIdentity() // this makes sure we are using a Roland
    {
        // see TD-27 MIDI implementation manual for details
        DeviceId = 0x10; // not sure why, this can be set on the module under SYSTEM - MIDI - BASIC
        var bytes = new byte[] {
            0xF0, 0x7E, DeviceId, 0x06, 0x01, 0xF7
        };
        var reply = await SendAndGetReply(bytes, e => e[0] == 0xF0 && e[1] == 0x7E && e[3] == 0x06 && e[4] == 0x02);

        DeviceId = reply[2];
        ManufacturerId = reply[6];
        Console.WriteLine($"Manufacturer: {ManufacturerId:x2}");
        ModelId = BitConverter.ToUInt32(reply, 7);
        // there's also software revision level in reply

        return RolandVerified ?? false;
    }

    Task<byte[]> SendAndGetReply(byte[] bytes, Func<byte[], bool> filter)
    {
        var replyTask = AsyncGetReply(filter);
        DebugBytes(bytes, "Sending bytes");
        SendBytes(bytes);
        return replyTask;
    }

    Task<byte[]> AsyncGetReply(Func<byte[], bool> filter = null)
    {
        var tcs = new TaskCompletionSource<byte[]>();
        void handler(byte[] bytes)
        {
            if (filter == null || filter(bytes))
            {
                tcs.TrySetResult(bytes);
                DrumMidiHandler.ExtraMidiHandler -= handler;
            }
        }
        DrumMidiHandler.ExtraMidiHandler += handler;
        return tcs.Task;
    }

    void SendBytes(byte[] bytes) => DrumMidiHandler.SendBytes(bytes);

    public static byte[] GetBytesBigEndian(int n)
    {
        return new byte[] { (byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n };
    }
    public static int GetInt(byte[] bytes, int position) // big endian
        => bytes[position] << 24 | bytes[position + 1] << 16 | bytes[position + 2] << 8 | bytes[position + 3];

    static byte checksum(int sum) => (byte)(((sum ^ 0x7F) + 1) & 0x7F);

    public async Task DumpBytes(int start, int length)
    {
        await EnsureRoland();
        var end = start + length;
        if (end > 1 << 20) throw new ArgumentException($"End too large: {end}");

        var modelBytes = BitConverter.GetBytes(ModelId);
        var addressBytes = GetBytesBigEndian(start);
        var sizeBytes = GetBytesBigEndian(length);

        var sum = addressBytes[0] + addressBytes[1] + addressBytes[2] + addressBytes[3]
             + sizeBytes[0] + sizeBytes[1] + sizeBytes[2] + sizeBytes[3];
        var bytes = new byte[] {
            0xF0, ManufacturerId, DeviceId,
            modelBytes[0], modelBytes[1], modelBytes[2], modelBytes[3],
            0x11,
            addressBytes[0], addressBytes[1], addressBytes[2], addressBytes[3],
            sizeBytes[0], sizeBytes[1], sizeBytes[2], sizeBytes[3],
            checksum(sum),
            0xF7
        };

        var reply = await SendAndGetReply(bytes, e => e[0] == 0xF0 && e[1] == ManufacturerId && e[2] == DeviceId && e[7] == 0x12
            && GetInt(e, 8) == start);

        // we know the address in reply matches our target address since we check in our filter

        if (memoryDump == null)
            memoryDump = new byte[end * 2];
        else if (memoryDump.Length < end)
        {
            var newBuffer = new byte[end * 2];
            memoryDump.CopyTo(newBuffer, 0);
            memoryDump = newBuffer;
        }
        Array.Copy(reply, 12, memoryDump, start, length);
    }

    public async Task EnsureRoland()
    {
        if (RolandVerified == null)
            await CheckRolandIdentity();
        if (!RolandVerified.Value) throw new Exception("Non-Roland module");
    }

    public void Dispose()
    {
        DrumMidiHandler.ExtraMidiHandler -= OnMidiEvent;
    }
}