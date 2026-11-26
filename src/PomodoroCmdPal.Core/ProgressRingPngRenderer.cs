using System.Buffers.Binary;
using System.IO.Compression;

namespace PomodoroCmdPal.Core;

/// <summary>
/// Zero-dependency anti-aliased RGBA PNG renderer for the Dock progress ring
/// with rounded arc end-caps, soft inner glow, final-minute breathing pulse,
/// and 5-frame completion celebration burst animation.
/// </summary>
public static class ProgressRingPngRenderer
{
    private const int DefaultSize = 48;

    public static byte[] Render(
        PomodoroPhase phase,
        PomodoroState state,
        double progress,
        bool finalMinutePulseHigh = false,
        bool isFinalMinute = false,
        int size = DefaultSize)
    {
        progress = Math.Clamp(progress, 0.0, 1.0);
        var (arcR, arcG, arcB) = GetAccentColor(phase, state, isFinalMinute);

        var cx = size / 2.0;
        var cy = size / 2.0;
        var outerR = size * 0.43;
        var innerR = size * 0.28;
        var midR = (outerR + innerR) * 0.5;
        var capR = (outerR - innerR) * 0.5;

        var stride = size * 4 + 1;
        var raw = new byte[stride * size];

        var sweepAngle = progress * 2.0 * Math.PI;
        var hasPartialArc = progress > 0.001 && progress < 0.999;

        // Rounded cap centers at start (12 o'clock) and end (sweepAngle clockwise)
        var startCapX = 0.0;
        var startCapY = -midR;
        var endCapX = Math.Sin(sweepAngle) * midR;
        var endCapY = -Math.Cos(sweepAngle) * midR;

        for (var y = 0; y < size; y++)
        {
            var rowStart = y * stride;
            raw[rowStart] = 0;

            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5 - cx;
                var dy = y + 0.5 - cy;
                var dist = Math.Sqrt(dx * dx + dy * dy);

                double outR = 0, outG = 0, outB = 0, outA = 0;

                // 1. Subtle soft ambient halo around the ring
                var haloDist = Math.Abs(dist - midR);
                var haloWidth = capR * (isFinalMinute && finalMinutePulseHigh ? 2.5 : 1.9);
                if (haloDist < haloWidth && state == PomodoroState.Running)
                {
                    var haloCov = (1.0 - haloDist / haloWidth);
                    haloCov = haloCov * haloCov * (isFinalMinute && finalMinutePulseHigh ? 0.24 : 0.14);
                    BlendOver(ref outR, ref outG, ref outB, ref outA, arcR, arcG, arcB, haloCov);
                }

                // 2. Background track ring
                var outerCov = SmoothStep(outerR + 0.75, outerR - 0.75, dist);
                var innerCov = SmoothStep(innerR - 0.75, innerR + 0.75, dist);
                var ringCov = outerCov * innerCov;

                if (ringCov > 0.001)
                {
                    BlendOver(ref outR, ref outG, ref outB, ref outA, 150, 155, 165, ringCov * 0.32);
                }

                // 3. Active progress arc with Apple Watch–style rounded end-caps
                double arcCov = 0.0;
                if (progress >= 0.999)
                {
                    arcCov = ringCov;
                }
                else if (hasPartialArc)
                {
                    var angle = Math.Atan2(dx, -dy);
                    if (angle < 0) angle += 2.0 * Math.PI;

                    if (angle <= sweepAngle)
                    {
                        arcCov = ringCov;
                    }

                    // Rounded caps at both arc endpoints
                    var dStart = Math.Sqrt((dx - startCapX) * (dx - startCapX) + (dy - startCapY) * (dy - startCapY));
                    var dEnd = Math.Sqrt((dx - endCapX) * (dx - endCapX) + (dy - endCapY) * (dy - endCapY));
                    var capCovStart = SmoothStep(capR + 0.75, capR - 0.75, dStart);
                    var capCovEnd = SmoothStep(capR + 0.75, capR - 0.75, dEnd);
                    arcCov = Math.Max(arcCov, Math.Max(capCovStart, capCovEnd));
                }

                if (arcCov > 0.001)
                {
                    BlendOver(ref outR, ref outG, ref outB, ref outA, arcR, arcG, arcB, arcCov);
                }

                // 4. Center state glyph (pause bars, play triangle, or breathing dot)
                if (dist < innerR - 0.5)
                {
                    var centerAlpha = SampleCenterGlyph(dx, dy, innerR, state, finalMinutePulseHigh, isFinalMinute);
                    if (centerAlpha > 0.001)
                    {
                        BlendOver(ref outR, ref outG, ref outB, ref outA, arcR, arcG, arcB, centerAlpha * 0.94);
                    }
                }

                var px = rowStart + 1 + x * 4;
                raw[px] = (byte)Math.Clamp(Math.Round(outR), 0, 255);
                raw[px + 1] = (byte)Math.Clamp(Math.Round(outG), 0, 255);
                raw[px + 2] = (byte)Math.Clamp(Math.Round(outB), 0, 255);
                raw[px + 3] = (byte)Math.Clamp(Math.Round(outA * 255.0), 0, 255);
            }
        }

        return EncodePng(raw, size, size);
    }

    /// <summary>
    /// Renders a frame of the completion celebration burst (expanding glow ring + crisp checkmark).
    /// </summary>
    public static byte[] RenderCelebrationFrame(PomodoroPhase completedPhase, int frameIndex, int totalFrames = 5, int size = DefaultSize)
    {
        var t = Math.Clamp((double)frameIndex / Math.Max(1, totalFrames - 1), 0.0, 1.0);
        var (accentR, accentG, accentB) = completedPhase == PomodoroPhase.Focus
            ? ((byte)52, (byte)211, (byte)153)   // Fresh Emerald (#34D399)
            : ((byte)108, (byte)140, (byte)255); // Calm Indigo (#6C8CFF)

        var cx = size / 2.0;
        var cy = size / 2.0;
        var baseR = size * 0.36;
        var burstR = size * (0.28 + 0.19 * t);
        var burstAlpha = (1.0 - t) * 0.85;
        var checkScale = 0.75 + 0.25 * Math.Sin(Math.Min(1.0, t * 1.4) * Math.PI * 0.5);

        var stride = size * 4 + 1;
        var raw = new byte[stride * size];

        // Checkmark segments in local coordinates
        var s = size * 0.22 * checkScale;
        var ax = -s * 0.85; var ay = s * 0.05;
        var bx = -s * 0.20; var by = s * 0.70;
        var ex = s * 0.95; var ey = -s * 0.65;
        var strokeHalf = size * 0.048;

        for (var y = 0; y < size; y++)
        {
            var rowStart = y * stride;
            raw[rowStart] = 0;

            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5 - cx;
                var dy = y + 0.5 - cy;
                var dist = Math.Sqrt(dx * dx + dy * dy);

                double outR = 0, outG = 0, outB = 0, outA = 0;

                // Filled soft badge
                var fillCov = SmoothStep(baseR + 0.8, baseR - 0.8, dist);
                if (fillCov > 0.001)
                {
                    BlendOver(ref outR, ref outG, ref outB, ref outA, accentR, accentG, accentB, fillCov * 0.26);
                }

                // Crisp outer circle
                var ringDist = Math.Abs(dist - baseR);
                var ringCov = SmoothStep(1.9, 0.7, ringDist);
                if (ringCov > 0.001)
                {
                    BlendOver(ref outR, ref outG, ref outB, ref outA, accentR, accentG, accentB, ringCov);
                }

                // Expanding celebration halo ring
                var haloDist = Math.Abs(dist - burstR);
                var haloCov = SmoothStep(2.6, 0.4, haloDist) * burstAlpha;
                if (haloCov > 0.001)
                {
                    BlendOver(ref outR, ref outG, ref outB, ref outA, accentR, accentG, accentB, haloCov);
                }

                // Anti-aliased checkmark
                var dCheck = Math.Min(
                    DistanceToSegment(dx, dy, ax, ay, bx, by),
                    DistanceToSegment(dx, dy, bx, by, ex, ey));
                var checkCov = SmoothStep(strokeHalf + 0.75, strokeHalf - 0.75, dCheck);
                if (checkCov > 0.001)
                {
                    BlendOver(ref outR, ref outG, ref outB, ref outA, 255, 255, 255, checkCov);
                }

                var px = rowStart + 1 + x * 4;
                raw[px] = (byte)Math.Clamp(Math.Round(outR), 0, 255);
                raw[px + 1] = (byte)Math.Clamp(Math.Round(outG), 0, 255);
                raw[px + 2] = (byte)Math.Clamp(Math.Round(outB), 0, 255);
                raw[px + 3] = (byte)Math.Clamp(Math.Round(outA * 255.0), 0, 255);
            }
        }

        return EncodePng(raw, size, size);
    }

    private static (byte R, byte G, byte B) GetAccentColor(PomodoroPhase phase, PomodoroState state, bool isFinalMinute)
    {
        if (state == PomodoroState.Paused)
        {
            return (245, 166, 35); // Warm Amber (#F5A623)
        }

        if (isFinalMinute && phase == PomodoroPhase.Focus)
        {
            return (255, 112, 67); // Bright Coral-Orange urgency (#FF7043)
        }

        return phase switch
        {
            PomodoroPhase.Focus => (232, 93, 74),       // Coral Red (#E85D4A)
            PomodoroPhase.ShortBreak => (46, 196, 182), // Mint Teal (#2EC4B6)
            PomodoroPhase.LongBreak => (108, 140, 255), // Calm Indigo (#6C8CFF)
            _ => (232, 93, 74)
        };
    }

    private static double SampleCenterGlyph(
        double dx,
        double dy,
        double innerR,
        PomodoroState state,
        bool finalMinutePulseHigh,
        bool isFinalMinute)
    {
        if (state == PomodoroState.Paused)
        {
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

        // Running: calm breathing dot (more pronounced in final minute)
        var dotScale = isFinalMinute
            ? (finalMinutePulseHigh ? 0.34 : 0.20)
            : (finalMinutePulseHigh ? 0.24 : 0.19);
        var dotR = innerR * dotScale;
        var d = Math.Sqrt(dx * dx + dy * dy);
        return SmoothStep(dotR + 0.65, dotR - 0.65, d);
    }

    private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        var abx = bx - ax;
        var aby = by - ay;
        var lenSq = abx * abx + aby * aby;
        if (lenSq <= 1e-9)
        {
            return Math.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));
        }

        var t = Math.Clamp(((px - ax) * abx + (py - ay) * aby) / lenSq, 0.0, 1.0);
        var projX = ax + t * abx;
        var projY = ay + t * aby;
        return Math.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
    }

    private static void BlendOver(
        ref double dstR,
        ref double dstG,
        ref double dstB,
        ref double dstA,
        double srcR,
        double srcG,
        double srcB,
        double srcA)
    {
        var outA = srcA + dstA * (1.0 - srcA);
        if (outA <= 1e-6)
        {
            dstR = dstG = dstB = dstA = 0;
            return;
        }

        dstR = (srcR * srcA + dstR * dstA * (1.0 - srcA)) / outA;
        dstG = (srcG * srcA + dstG * dstA * (1.0 - srcA)) / outA;
        dstB = (srcB * srcA + dstB * dstA * (1.0 - srcA)) / outA;
        dstA = outA;
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
        ihdr[8] = 8;
        ihdr[9] = 6;
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
