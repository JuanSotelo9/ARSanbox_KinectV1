using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace ProyectoSandbox
{
    /// <summary>
    /// Escribe float[307200] en memoria compartida y señala a Unity vía un EventWaitHandle.
    ///
    /// Layout del bloque compartido (1 228 804 bytes):
    ///   [0..3]          uint   frameId   — número de frame incremental (little-endian)
    ///   [4..1228803]    float* depth     — 307 200 floats × 4 bytes (little-endian)
    ///
    /// Sincronización:
    ///   El sender pulsa "SandboxDepthReady" (EventWaitHandle auto-reset) cada vez
    ///   que termina de escribir un frame completo.  Unity espera ese evento en su
    ///   hilo de recepción, lee la memoria y vuelve a esperar.
    ///   No hay chunks, no hay sockets, no hay copias extra.
    /// </summary>
    public class SharedMemoryDepthSender : IDisposable
    {
        // ── Nombres compartidos (deben coincidir con TerrainGenerator) ────────
        public const string MmfName   = "SandboxDepthMap";
        public const string EventName = "SandboxDepthReady";

        // ── Layout ────────────────────────────────────────────────────────────
        private const int FrameIdOffset  = 0;
        private const int DepthOffset    = 4;
        private const int TotalPixels    = 307_200;
        private const long MmfSize       = DepthOffset + TotalPixels * 4L; // 1 228 804 bytes

        // ── Estado ────────────────────────────────────────────────────────────
        private readonly MemoryMappedFile       _mmf;
        private readonly MemoryMappedViewAccessor _view;
        private readonly EventWaitHandle        _readyEvent;
        private uint   _frameId;
        private bool   _disposed;

        public SharedMemoryDepthSender()
        {
            // CreateOrOpen permite que el sender arranque antes o después que Unity
            _mmf  = MemoryMappedFile.CreateOrOpen(MmfName, MmfSize,
                        MemoryMappedFileAccess.ReadWrite);
            _view = _mmf.CreateViewAccessor(0, MmfSize,
                        MemoryMappedFileAccess.ReadWrite);

            _readyEvent = new EventWaitHandle(
                initialState: false,
                mode:         EventResetMode.AutoReset,
                name:         EventName);
        }

        /// <summary>
        /// Llama esto dentro de OnFrameReady (hilo del sensor).
        /// Copia el array completo en una sola operación y señala a Unity.
        /// </summary>
        public unsafe void Send(float[] depth)
        {
            if (_disposed) return;
            if (depth == null || depth.Length < TotalPixels)
                throw new ArgumentException($"Se esperan {TotalPixels} floats.", nameof(depth));

            // Escribir frameId
            _view.Write(FrameIdOffset, _frameId);

            // Escribir los floats directamente (cero-copy vía puntero interno del accessor)
            // WriteArray es la API gestionada más rápida disponible en .NET Standard 2.0
            _view.WriteArray(DepthOffset, depth, 0, TotalPixels);

            // Garantizar que las escrituras sean visibles antes de señalar
            _view.Flush();

            _frameId++;

            // Notificar a Unity
            _readyEvent.Set();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _readyEvent.Dispose();
            _view.Dispose();
            _mmf.Dispose();
        }
    }
}
