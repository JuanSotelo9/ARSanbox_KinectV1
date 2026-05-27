using System;
using System.Collections.Generic;
using OpenCvSharp;
using Microsoft.Kinect;

namespace ProyectoSandbox
{
    /// <summary>
    /// Detecta la mano usando el stream de PROFUNDIDAD del Kinect + OpenCvSharp4.
    /// No requiere ver el cuerpo completo — funciona en configuración top-down (sandbox).
    ///
    /// Algoritmo:
    ///   1. Recibe short[] rawDepth del Kinect (valores >> 3 = mm)
    ///   2. Umbraliza: solo píxeles entre MinHandMm y MaxHandMm (la mano es lo más cercano)
    ///   3. Morphology open/close para limpiar ruido
    ///   4. Busca el contorno más grande → silueta de la mano
    ///   5. Convex hull + defectos de convexidad → conteo de dedos
    ///   6. Centroide → posición XY normalizada
    ///   7. Historial de posiciones → detección de swipe
    /// </summary>
    public class OpenCvHandTracker : IDisposable
    {
        // ── Configuración pública ──────────────────────────────────────────────

        /// <summary>Profundidad mínima de la mano en mm (por encima de la arena).</summary>
        public int MinHandMm { get; set; } = 800;

        /// <summary>Profundidad máxima de la mano en mm. 
        /// Ajusta según cuánto eleva la mano sobre la superficie.</summary>
        public int MaxHandMm { get; set; } = 950;

        /// <summary>Área mínima del contorno (píxeles²) para considerar que es una mano.</summary>
        public double MinContourArea { get; set; } = 3000;

        /// <summary>Región de interés — mismo crop que KinectDepthReader.</summary>
        public int CropXStart { get; set; } = 50;
        public int CropXEnd { get; set; } = 590;
        public int CropYStart { get; set; } = 30;
        public int CropYEnd { get; set; } = 420;

        // ── Eventos ───────────────────────────────────────────────────────────
        public event EventHandler<HandData> HandReady;

        // ── Internals ─────────────────────────────────────────────────────────
        private const int W = 640;
        private const int H = 480;

        // Para detección de swipe
        private readonly Queue<Point2f> _posHistory = new Queue<Point2f>();
        private const int SwipeHistoryFrames = 12;   // ~400 ms a 30 fps
        private const float SwipeThreshold = 0.18f; // 18% del ancho/alto del frame

        // Buffers reutilizables (evitar GC pressure)
        private Mat _depthMat;    // CV_16U — valores en mm
        private Mat _threshMat;   // CV_8U — máscara binaria
        private Mat _kernel;

        public OpenCvHandTracker()
        {
            _depthMat = new Mat(H, W, MatType.CV_16UC1);
            _threshMat = new Mat(H, W, MatType.CV_8UC1);
            _kernel = Cv2.GetStructuringElement(
                             MorphShapes.Ellipse, new Size(7, 7));
        }

        // ── API pública ───────────────────────────────────────────────────────

        /// <summary>
        /// Llamar desde DepthFrameReady del Kinect.
        /// rawData: array short[] sin procesar (incluye player index bits).
        /// </summary>
        public void ProcessFrame(short[] rawData)
        {
            if (rawData == null || rawData.Length != W * H) return;

            // 1. Construir Mat de profundidad en mm (CV_16U)
            BuildDepthMat(rawData);

            // 2. Umbralizar: mano = objeto más cercano en el rango definido
            //    Cv2.InRange trabaja sobre valores ushort directamente
            Cv2.InRange(
                _depthMat,
                new Scalar(MinHandMm),
                new Scalar(MaxHandMm),
                _threshMat);

            // 3. Aplicar ROI mask (zona fuera del crop → negro)
            ApplyCropMask(_threshMat);

            // 4. Morphology: eliminar ruido pequeño y rellenar huecos
            Cv2.MorphologyEx(_threshMat, _threshMat, MorphTypes.Open, _kernel);
            Cv2.MorphologyEx(_threshMat, _threshMat, MorphTypes.Close, _kernel);

            // 5. Encontrar contornos
            Cv2.FindContours(
                _threshMat,
                out Point[][] contours,
                out _,
                RetrievalModes.External,
                ContourApproximationModes.ApproxSimple);

            if (contours == null || contours.Length == 0)
            {
                EmitNotTracked();
                return;
            }

            // 6. Tomar el contorno de mayor área
            int bestIdx = 0;
            double bestArea = 0;
            for (int i = 0; i < contours.Length; i++)
            {
                double a = Cv2.ContourArea(contours[i]);
                if (a > bestArea) { bestArea = a; bestIdx = i; }
            }

            if (bestArea < MinContourArea)
            {
                EmitNotTracked();
                return;
            }

            Point[] handContour = contours[bestIdx];

            // 7. Centroide
            Moments mom = Cv2.Moments(handContour);
            if (mom.M00 < 1) { EmitNotTracked(); return; }

            float cx = (float)(mom.M10 / mom.M00);
            float cy = (float)(mom.M01 / mom.M00);

            // Normalizar dentro del ROI
            float nx = Normalize(cx, CropXStart, CropXEnd);
            float ny = Normalize(cy, CropYStart, CropYEnd);

            // 8. Profundidad media en el bounding rect de la mano
            Rect br = Cv2.BoundingRect(handContour);
            float zMm = MeanDepthInRect(rawData, br);
            float zM = zMm / 1000f;

            // 9. Contar dedos via defectos de convexidad
            int fingers = CountFingers(handContour, bestArea);
            HandState state = ClassifyState(fingers);

            // 10. Swipe
            SwipeDirection swipe = DetectSwipe(nx, ny);

            // 11. Emitir
            HandReady?.Invoke(this, new HandData
            {
                IsTracked = true,
                IsRight = true,   // top-down: no distinguimos lateralidad
                X = nx,
                Y = ny,
                Z = zM,
                Fingers = fingers,
                State = state,
                Swipe = swipe
            });
        }

        // ── Helpers privados ──────────────────────────────────────────────────

        private unsafe void BuildDepthMat(short[] rawData)
        {
            // Copiar valores en mm al Mat. Usamos unsafe para velocidad.
            ushort* ptr = (ushort*)_depthMat.DataPointer;
            for (int i = 0; i < rawData.Length; i++)
            {
                // Kinect SDK 1.8: desplazar 3 bits para obtener mm
                ptr[i] = (ushort)(rawData[i] >> DepthImageFrame.PlayerIndexBitmaskWidth);
            }
        }

        private void ApplyCropMask(Mat mask)
        {
            // Poner a cero todo lo que esté fuera del ROI
            // Fila superior
            if (CropYStart > 0)
                mask.RowRange(0, CropYStart).SetTo(Scalar.Black);
            // Fila inferior
            if (CropYEnd < H - 1)
                mask.RowRange(CropYEnd + 1, H).SetTo(Scalar.Black);
            // Columna izquierda
            if (CropXStart > 0)
                mask.ColRange(0, CropXStart).SetTo(Scalar.Black);
            // Columna derecha
            if (CropXEnd < W - 1)
                mask.ColRange(CropXEnd + 1, W).SetTo(Scalar.Black);
        }

        private int CountFingers(Point[] contour, double contourArea)
        {
            // Convex hull (índices)
            int[] hullIdx = Cv2.ConvexHullIndices(contour);
            if (hullIdx.Length < 3) return 0;

            // Defectos de convexidad
            Vec4i[] defects;
            try { defects = Cv2.ConvexityDefects(contour, hullIdx); }
            catch { return 0; }

            if (defects == null || defects.Length == 0) return 0;

            // Filtrar defectos significativos
            // Vec4i: [start_idx, end_idx, farthest_idx, depth/256]
            double handSize = Math.Sqrt(contourArea);
            double minDepth = handSize * 0.20; // 20% del tamaño estimado de la mano
            double maxDepth = handSize * 0.90;

            int gaps = 0; // gaps entre dedos = defectos profundos
            foreach (Vec4i d in defects)
            {
                double depth = d.Item3 / 256.0; // profundidad en píxeles
                if (depth < minDepth || depth > maxDepth) continue;

                // Ángulo en el punto más lejano (vértice del triángulo)
                Point s = contour[d.Item0]; // start
                Point e = contour[d.Item1]; // end
                Point f = contour[d.Item2]; // far (fondo del valle)

                double angle = AngleDeg(s, f, e);
                if (angle < 90.0) // ángulo agudo → espacio entre dedos
                    gaps++;
            }

            // gaps entre dedos → número de dedos = gaps + 1, máximo 5
            return Math.Min(gaps + 1, 5);
        }

        private static double AngleDeg(Point a, Point b, Point c)
        {
            double ba = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
            double bc = Math.Sqrt(Math.Pow(c.X - b.X, 2) + Math.Pow(c.Y - b.Y, 2));
            double ac = Math.Sqrt(Math.Pow(a.X - c.X, 2) + Math.Pow(a.Y - c.Y, 2));
            double cos = (ba * ba + bc * bc - ac * ac) / (2 * ba * bc + 1e-6);
            return Math.Acos(Math.Max(-1, Math.Min(1, cos))) * 180.0 / Math.PI;
        }

        private static HandState ClassifyState(int fingers)
        {
            // 0-1 dedos → cerrada, 2-3 → lasso, 4-5 → abierta
            if (fingers <= 1) return HandState.Closed;
            if (fingers <= 3) return HandState.Lasso;
            return HandState.Open;
        }

        private SwipeDirection DetectSwipe(float nx, float ny)
        {
            _posHistory.Enqueue(new Point2f(nx, ny));
            if (_posHistory.Count > SwipeHistoryFrames)
                _posHistory.Dequeue();

            if (_posHistory.Count < SwipeHistoryFrames)
                return SwipeDirection.None;

            // Comparar primer y último punto del historial
            Point2f[] arr = _posHistory.ToArray();
            float dx = arr[arr.Length - 1].X - arr[0].X;
            float dy = arr[arr.Length - 1].Y - arr[0].Y;

            if (Math.Abs(dx) < SwipeThreshold && Math.Abs(dy) < SwipeThreshold)
                return SwipeDirection.None;

            // Determinar dirección dominante
            if (Math.Abs(dx) >= Math.Abs(dy))
                return dx > 0 ? SwipeDirection.Right : SwipeDirection.Left;
            else
                return dy > 0 ? SwipeDirection.Down : SwipeDirection.Up;
        }

        private float MeanDepthInRect(short[] rawData, Rect r)
        {
            long sum = 0;
            int count = 0;
            int x0 = Math.Max(r.X, 0), x1 = Math.Min(r.X + r.Width, W);
            int y0 = Math.Max(r.Y, 0), y1 = Math.Min(r.Y + r.Height, H);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int mm = rawData[y * W + x] >> DepthImageFrame.PlayerIndexBitmaskWidth;
                    if (mm > 0) { sum += mm; count++; }
                }
            return count > 0 ? (float)sum / count : 0f;
        }

        private static float Normalize(float val, int min, int max)
            => Math.Max(0f, Math.Min(1f, (val - min) / (float)(max - min)));

        private void EmitNotTracked()
        {
            _posHistory.Clear();
            HandReady?.Invoke(this, new HandData { IsTracked = false });
        }

        public void Dispose()
        {
            _depthMat?.Dispose();
            _threshMat?.Dispose();
            _kernel?.Dispose();
        }
    }
}