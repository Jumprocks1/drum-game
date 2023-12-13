using System.Collections.Generic;

namespace DrumGame.Game.Media;

public class BufferBag
{
    List<byte[]> Buffers = new();
    public byte[] GetBuffer(int size) // make sure to add to Buffers after we're done
    {
        lock (Buffers)
        {
            for (var i = 0; i < Buffers.Count; i++)
            {
                var buffer = Buffers[i];
                if (buffer.Length >= size)
                {
                    Buffers.RemoveAt(i);
                    return buffer;
                }
            }
        }
        return new byte[size];
    }

    public void ReturnBuffer(byte[] buffer)
    {
        lock (Buffers) Buffers.Add(buffer);
    }
}