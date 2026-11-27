using System.Buffers.Binary;
using System.IO.Compression;

namespace Blastzone.Engine.Render;

/// <summary>Writes 8-bit RGBA PNG files (for screenshots), using only the BCL.</summary>
public static class Png
{
    public static void WriteRgba(string path, int width, int height, byte[] pixels, bool swapRedBlue)
    {
        var raw = new byte[height * (width * 4 + 1)];
        for (var y = 0; y < height; y++)
        {
            var row = y * (width * 4 + 1);
            raw[row] = 0; // no filter
            for (var x = 0; x < width; x++)
            {
                var src = (y * width + x) * 4;
                var dst = row + 1 + x * 4;
                raw[dst] = pixels[src + (swapRedBlue ? 2 : 0)];
                raw[dst + 1] = pixels[src + 1];
                raw[dst + 2] = pixels[src + (swapRedBlue ? 0 : 2)];
                raw[dst + 3] = 255;
            }
        }

        using var file = File.Create(path);
        file.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8; // bit depth
        header[9] = 6; // RGBA
        WriteChunk(file, "IHDR", header);
        using (var compressed = new MemoryStream())
        {
            using (var z = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
                z.Write(raw);
            WriteChunk(file, "IDAT", compressed.ToArray());
        }
        WriteChunk(file, "IEND", []);
    }

    static void WriteChunk(Stream stream, string type, byte[] data)
    {
        Span<byte> four = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(four, data.Length);
        stream.Write(four);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crc = Crc32(typeBytes, data);
        BinaryPrimitives.WriteUInt32BigEndian(four, crc);
        stream.Write(four);
    }

    static uint Crc32(byte[] type, byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        void Add(byte[] bytes)
        {
            foreach (var b in bytes)
            {
                crc ^= b;
                for (var k = 0; k < 8; k++)
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }
        Add(type);
        Add(data);
        return ~crc;
    }
}
