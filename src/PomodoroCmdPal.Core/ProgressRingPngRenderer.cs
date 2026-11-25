using System.Buffers.Binary;
using System.IO.Compression;

namespace PomodoroCmdPal.Core;

/// <summary>
/// Zero-dependency anti-aliased RGBA PNG renderer for the Dock progress ring icon.
/// </summary>
public static class ProgressRingPngRenderer
{
    private const int DefaultSize = 48;

    public static byte[] Render(PomodoroPhase phase, PomodoroState state, double progress, int size = DefaultSize)
    {
        progress = Math.Clamp(progress, 0.0, 1.0);
        var (arcR, arcG, arcB) = GetAccentColor(phase, state);

        var cx = size / 2.0;
        var cy = size / 2.0;
        var outerR = size * 0.44;
        var innerR = size * 0.29;
        var stride = size * 4 + 1;
        var raw = new byte[stride * size];

        // Clockwise sweep starting from 12 o'clock (-PI/2)
        var sweepAngle = progress * 2.0 * Math.PI;

        for (var y = 0; y < size; y++)
        {
            var rowStart = y * stride;
            raw[rowStart] = 0; // PNG filter type 0 (None)

            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5 - cx;
                var dy = y + 0.5 - cy;
                var dist = Math.Hypot(dx, dy);

                byte r = 0, g = 0, b = 0, a = 0;

                // Anti-aliased ring coverage
                var outerCov = SmoothStep(outerR + 0.75, outerR - 0.75, dist);
                var innerCov = SmoothStep(innerR - 0.75, innerR + 0.75, dist);
                var ringCov = outerCov * innerCov;

                if (ringCov > 0.001)
                {
                    // Angle clockwise from top (0 .. 2*PI)
                    var angle = Math.Atan2(dx, -dy);
                    if (angle < 0) angle += 2.0 * Math.PI;

                    if (progress >= 0.999 || (progress > 0.001 && angle <= sweepAngle))
                    {
                        r = arcR;
                        g = arcG;
                        b = arcB;
                        a = (byte)Math.Clamp(Math.Round(ringCov * 255.0), 0, 255);
                    }
                    else
                    {
                        // Subtle translucent track ring visible on both dark & light themes
                        r = 150;
                        g = 155;
                        b = 165;
                        a = (byte)Math.Clamp(Math.Round(ringCov * 85.0), 0, 255);
                    }
                }
                else if (dist < innerR - 1.0)
                {
                    // Center state glyph (pause bars when paused, play triangle when idle, tiny dot when running)
                    var centerAlpha = SampleCenterGlyph(dx, dy, innerR, state);
                    if (centerAlpha > 0.001)
                    {
                        r = arcR;
                        g = arcG;
                        b = arcB;
                        a = (byte)Math.Clamp(Math.Round(centerAlpha * 235.0), 0, 255);
                    }
                }

                var px = rowStart + 1 + x * 4;
                raw[px] = r;
                raw[px + 1] = g;
                raw[px + 2] = b;
                raw[px + 3] = a;
            }
        }

        return EncodePng(raw, size, size);
    }

    private static (byte R, byte G, byte B) GetAccentColor(PomodoroPhase phase, PomodoroState state)
    {
        if (state == PomodoroState.Paused)
        {
            return (245, 166, 35); // Warm Amber (#F5A623)
        }

        return phase switch
        {
            PomodoroPhase.Focus => (232, 93, 74),       // Coral Red (#E85D4A)
            PomodoroPhase.ShortBreak => (46, 196, 182), // Mint Teal (#2EC4B6)
            PomodoroPhase.LongBreak => (108, 140, 255), // Calm Indigo (#6C8CFF)
            _ => (232, 93, 74)
        };
    }

    private static double SampleCenterGlyph(double dx, double dy, double innerR, PomodoroState state)
    {
        if (state == PomodoroState.Paused)
        {
            // Two crisp vertical pause bars
            var halfH = innerR * 0.44;
            var barW = innerR * 0.16;
            var barOffset = innerR * 0.24;
            if (Math.Abs(dy) <= halfH &&
                (Math.Abs(dx + barOffset) <= barW || Math.Abs(dx - barOffset) <= barW))
            {
                return 1.0;
            }

            return 0.0;
        }

        if (state == PomodoroState.Idle)
        {
            // Minimal play triangle pointing right
            var left = -innerR * 0.28;
            var right = innerR * 0.40;
            if (dx >= left && dx <= right)
            {
                var t = (dx - left) / (right - left);
                var maxHalfH = innerR * 0.44 * (1.0 - t);
                if (Math.Abs(dy) <= maxHalfH)
                {
                    return 1.0;
                }
            }

            return 0.0;
        }

        // Running: tiny calm center dot
        var dotR = innerR * 0.22;
        var d = Math.Hypot(dx, dy);
        return SmoothStep(dotR + 0.6, dotR - 0.6, d);
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0.0, 1.0);
        return t * t * (3.0 - 2.0 * t);
    }

    private static byte[] EncodePng(byte[] rawScanlines, int width, int height)
    {
        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4, 4), height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // RGBA
        WriteChunk(ms, "IHDR"u8, ihdr);

        byte[] compressed;
        using (var compMs = new MemoryStream())
        {
            using (var zlib = new ZLibStream(compMs, CompressionLevel.Fastest, leaveOpen: true))
            {
                zlib.Write(rawScanlines, 0, rawScanlines.Length);
            }

            compressed = compMs.ToArray();
        }

        WriteChunk(ms, "IDAT"u8, compressed);
        WriteChunk(ms, "IEND"u8, []);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream output, ReadOnlySpan<byte> tag, ReadOnlySpan<byte> data)
    {
        Span<byte> lenBuf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(lenBuf, data.Length);
        output.Write(lenBuf);
        output.Write(tag);
        output.Write(data);

        var crc = ComputeCrc32(tag, data);
        Span<byte> crcBuf = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBuf, crc);
        output.Write(crcBuf);
    }

    private static uint ComputeCrc32(ReadOnlySpan<byte> tag, ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var b in tag) crc = UpdateCrc(crc, b);
        foreach (var b in data) crc = UpdateCrc(crc, b);
        return crc ^ 0xFFFFFFFF;
    }

    private static uint UpdateCrc(uint crc, byte b)
    {
        crc ^= b;
        for (var i = 0; i < 8; i++)
        {
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }

        return crc;
    }
}
