using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace ProjectAgil.Services;

public static class MinecraftPing
{
    public const double TimedOut = -1d;
    public const double Refused = -2d;

    private const int ProtocolVersion = 767;

    public static async Task<double> MeasureAsync(string host, int port, int timeoutMs, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(timeoutMs);

        try
        {
            using var client = new TcpClient { NoDelay = true };
            await client.ConnectAsync(host, port, timeout.Token).ConfigureAwait(false);

            var stream = client.GetStream();

            var handshake = new List<byte>();
            WriteVarInt(handshake, 0x00);
            WriteVarInt(handshake, ProtocolVersion);
            WriteString(handshake, host);
            handshake.Add((byte)(port >> 8));
            handshake.Add((byte)(port & 0xFF));
            WriteVarInt(handshake, 1);

            await SendAsync(stream, handshake, timeout.Token).ConfigureAwait(false);

            var request = new List<byte>();
            WriteVarInt(request, 0x00);
            await SendAsync(stream, request, timeout.Token).ConfigureAwait(false);

            await SkipPacketAsync(stream, timeout.Token).ConfigureAwait(false);

            var ping = new List<byte>();
            WriteVarInt(ping, 0x01);

            var stamp = BitConverter.GetBytes(DateTime.UtcNow.Ticks);
            Array.Reverse(stamp);
            ping.AddRange(stamp);

            var watch = Stopwatch.StartNew();
            await SendAsync(stream, ping, timeout.Token).ConfigureAwait(false);
            await SkipPacketAsync(stream, timeout.Token).ConfigureAwait(false);
            watch.Stop();

            return watch.Elapsed.TotalMilliseconds;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return TimedOut;
        }
        catch (SocketException)
        {
            return Refused;
        }
        catch (IOException)
        {
            return Refused;
        }
        catch
        {
            return TimedOut;
        }
    }

    private static async Task SendAsync(NetworkStream stream, List<byte> body, CancellationToken ct)
    {
        var frame = new List<byte>(body.Count + 5);
        WriteVarInt(frame, body.Count);
        frame.AddRange(body);

        await stream.WriteAsync(frame.ToArray(), ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task SkipPacketAsync(NetworkStream stream, CancellationToken ct)
    {
        var length = await ReadVarIntAsync(stream, ct).ConfigureAwait(false);

        if (length is <= 0 or > 1024 * 128)
        {
            throw new IOException("unexpected packet length");
        }

        var buffer = new byte[Math.Min(length, 4096)];
        var remaining = length;

        while (remaining > 0)
        {
            var chunk = Math.Min(remaining, buffer.Length);
            var read = await stream.ReadAsync(buffer.AsMemory(0, chunk), ct).ConfigureAwait(false);

            if (read == 0)
            {
                throw new IOException("connection closed");
            }

            remaining -= read;
        }
    }

    private static async Task<int> ReadVarIntAsync(NetworkStream stream, CancellationToken ct)
    {
        var result = 0;
        var shift = 0;
        var single = new byte[1];

        while (shift < 35)
        {
            var read = await stream.ReadAsync(single.AsMemory(0, 1), ct).ConfigureAwait(false);

            if (read == 0)
            {
                throw new IOException("connection closed");
            }

            result |= (single[0] & 0x7F) << shift;

            if ((single[0] & 0x80) == 0)
            {
                return result;
            }

            shift += 7;
        }

        throw new IOException("malformed length");
    }

    private static void WriteVarInt(List<byte> buffer, int value)
    {
        var v = (uint)value;

        while (true)
        {
            if ((v & ~0x7Fu) == 0)
            {
                buffer.Add((byte)v);
                return;
            }

            buffer.Add((byte)((v & 0x7F) | 0x80));
            v >>= 7;
        }
    }

    private static void WriteString(List<byte> buffer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarInt(buffer, bytes.Length);
        buffer.AddRange(bytes);
    }
}
