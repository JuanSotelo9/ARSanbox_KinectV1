using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace ProyectoSandbox
{
    /// <summary>
    /// Serializa float[307200] en paquetes UDP de tamaño fijo y los envía a Unity.
    /// Protocolo: cada paquete lleva un header de 8 bytes seguido de floats en raw bytes.
    ///
    ///   Header (8 bytes):
    ///     [0..3]  uint  frameId       — número de frame incremental
    ///     [4]     byte  totalChunks   — cuántos paquetes forman este frame
    ///     [5]     byte  chunkIndex    — índice de este paquete (0-based)
    ///     [6..7]  short chunkPixels   — cuántos floats lleva este paquete
    ///
    ///   Payload: chunkPixels × 4 bytes (floats en little-endian)
    /// </summary>
    public class UdpDepthSender : IDisposable
    {
        // ── Configuración ─────────────────────────────────────────────────────
        private const int MaxUdpPayload = 65000;          // bytes por paquete (seguro)
        private const int HeaderSize = 8;              // bytes del header
        private const int FloatsPerChunk =
            (MaxUdpPayload - HeaderSize) / 4;             // floats por paquete ≈ 16248

        private readonly UdpClient _client;
        private readonly IPEndPoint _endpoint;
        private uint _frameId;
        private bool _disposed;

        // Buffer reutilizable (evita alloc por frame)
        private readonly byte[] _sendBuffer =
            new byte[HeaderSize + FloatsPerChunk * 4];

        public UdpDepthSender(string host = "127.0.0.1", int port = 7777)
        {
            _client = new UdpClient();
            _endpoint = new IPEndPoint(IPAddress.Parse(host), port);
        }

        /// <summary>
        /// Llama esto dentro de OnFrameReady. Thread-safe: usa el hilo del sensor.
        /// </summary>
        public void Send(float[] depth)
        {
            if (_disposed) return;

            int totalFloats = depth.Length;                          // 307 200
            int totalChunks = (int)Math.Ceiling(
                (double)totalFloats / FloatsPerChunk);                // ≈ 19
            byte chunkCount = (byte)Math.Min(totalChunks, 255);

            for (int c = 0; c < totalChunks; c++)
            {
                int floatOffset = c * FloatsPerChunk;
                int floatsNow = Math.Min(FloatsPerChunk,
                                           totalFloats - floatOffset);

                // ── Header ────────────────────────────────────────────────────
                WriteUInt32(_sendBuffer, 0, _frameId);
                _sendBuffer[4] = chunkCount;
                _sendBuffer[5] = (byte)c;
                WriteInt16(_sendBuffer, 6, (short)floatsNow);

                // ── Payload: floats → bytes (little-endian) ───────────────────
                Buffer.BlockCopy(depth, floatOffset * 4,
                                 _sendBuffer, HeaderSize,
                                 floatsNow * 4);

                int packetSize = HeaderSize + floatsNow * 4;
                _client.Send(_sendBuffer, packetSize, _endpoint);
            }

            _frameId++;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _client.Dispose();
        }

        // ── Helpers de serialización (sin dependencias externas) ─────────────
        private static void WriteUInt32(byte[] buf, int offset, uint v)
        {
            buf[offset] = (byte)v;
            buf[offset + 1] = (byte)(v >> 8);
            buf[offset + 2] = (byte)(v >> 16);
            buf[offset + 3] = (byte)(v >> 24);
        }

        private static void WriteInt16(byte[] buf, int offset, short v)
        {
            buf[offset] = (byte)v;
            buf[offset + 1] = (byte)(v >> 8);
        }
    }
}