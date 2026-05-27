using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Kinect;

namespace ProyectoSandbox
{
    public partial class MainWindow : Window
    {
        private readonly KinectDepthReader _reader = new KinectDepthReader();
        private readonly SharedMemoryDepthSender _shmDepth = new SharedMemoryDepthSender();
        private readonly UdpHandSender _udpHand = new UdpHandSender("127.0.0.1", 5001);
        private OpenCvHandTracker _cvTracker;
        // Renderizado
        private WriteableBitmap _bitmap;
        private byte[] _bgraBuffer;
        private ColorMapType _colorMap = ColorMapType.Grayscale;

        private const int W = 640;
        private const int H = 480;
        private const int PixelCount = W * H;

        // FPS
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private int _frameCount;
        private double _fps;
        private int _totalFrames;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _bitmap = new WriteableBitmap(W, H, 96, 96, PixelFormats.Bgr32, null);
            DepthImage.Source = _bitmap;
            _bgraBuffer = new byte[PixelCount * 4];
            SetLegend(Colors.Black, Colors.White);

            _reader.FrameReady += OnFrameReady;

            try
            {
                _reader.Start();
                Console.WriteLine("✓ KinectDepthReader iniciado");

                // HandTracker usa el mismo sensor ya iniciado
                _cvTracker = new OpenCvHandTracker
                {
                    // Ajusta estos valores en laboratorio según tu setup físico
                    MinHandMm = 800,   // mm: techo de la arena hasta donde empieza a detectar
                    MaxHandMm = 950,   // mm: hasta dónde sube la mano sobre la arena
                    MinContourArea = 3000,
                    CropXStart = _reader.CropXStart,
                    CropXEnd = _reader.CropXEnd,
                    CropYStart = _reader.CropYStart,
                    CropYEnd = _reader.CropYEnd,
                };
                _cvTracker.HandReady += OnHandReady;
                _reader.RawFrameReady += (s, raw) => _cvTracker.ProcessFrame(raw);

                Console.WriteLine("✓ HandTracker iniciado");

                SetConnected(true);
                SetStatus("Kinect conectado · depth :5000 · hand :5001");
            }
            catch (Exception ex)
            {
                SetConnected(false);
                SetStatus($"Error: {ex.Message}");
                Console.WriteLine($"Error al iniciar: {ex}");
            }
        }

        

        // ── Frame de profundidad ──────────────────────────────────────────────
        private void OnFrameReady(object sender, float[] depth)
        {
            _shmDepth.Send(depth);

            _frameCount++;
            _totalFrames++;
            double elapsed = _sw.Elapsed.TotalSeconds;
            if (elapsed >= 1.0)
            {
                _fps = _frameCount / elapsed;
                _frameCount = 0;
                _sw.Restart();
            }

            DepthColorMapper.WriteToBuffer(depth, _bgraBuffer, _colorMap);

            float center = depth[PixelCount / 2];
            float dmin = float.MaxValue;
            float dmax = 0f;
            for (int i = 0; i < depth.Length; i++)
            {
                float v = depth[i];
                if (v <= 0f) continue;
                if (v < dmin) dmin = v;
                if (v > dmax) dmax = v;
            }

            double fps = _fps;
            int frames = _totalFrames;
            float mn = dmin == float.MaxValue ? 0f : dmin;
            float mx = dmax;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                _bitmap.Lock();
                _bitmap.WritePixels(new Int32Rect(0, 0, W, H), _bgraBuffer, W * 4, 0);
                _bitmap.AddDirtyRect(new Int32Rect(0, 0, W, H));
                _bitmap.Unlock();

                TxtFps.Text = $"{fps:F1}";
                TxtFrame.Text = frames.ToString();
                TxtCenter.Text = center > 0f ? $"{(center * 3.2f + 0.8f):F2} m" : "—";
                TxtMin.Text = mn > 0f ? $"{(mn * 3.2f + 0.8f):F2} m" : "—";
                TxtMax.Text = mx > 0f ? $"{(mx * 3.2f + 0.8f):F2} m" : "—";
            }));
        }

        // ── Frame de mano ─────────────────────────────────────────────────────
        private void OnHandReady(object sender, HandData hand)
        {
            // Enviar a Unity siempre (trackeado o no, Unity decide qué hacer)
            _udpHand.Send(hand);

            // Actualizar UI con estado de la mano
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (hand.IsTracked)
                {
                    string side = hand.IsRight ? "D" : "I";
                    string state;
                    switch (hand.State)
                    {
                        case HandState.Open:
                            state = "Abierta";
                            break;
                        case HandState.Closed:
                            state = "Cerrada";
                            break;
                        default:
                            state = "?";
                            break;
                    }
                    Console.WriteLine($"Mano {side}: X={hand.X:F2}, Y={hand.Y:F2}, Z={hand.Z:F2} m");
                    SetStatus($"Mano {side}: ({hand.X:F2}, {hand.Y:F2}, {hand.Z:F2}m) · {state}");
                }
                else
                {
                    Console.WriteLine("No hay mano detectada");
                    SetStatus("Kinect conectado · sin mano detectada");
                }
            }));
        }

        // ── Mapa de color ─────────────────────────────────────────────────────
        private void MapBtn_Checked(object sender, RoutedEventArgs e)
        {
            if (LegendContainer == null) return;
            var btn = sender as System.Windows.Controls.RadioButton;
            if (btn?.Tag == null) return;

            if (Enum.TryParse(btn.Tag.ToString(), out ColorMapType map))
            {
                _colorMap = map;
                switch (map)
                {
                    case ColorMapType.Grayscale:
                        SetLegend(Colors.Black, Colors.White); break;
                    case ColorMapType.Inverse:
                        SetLegend(Colors.White, Colors.Black); break;
                    case ColorMapType.Jet:
                        SetLegend(Color.FromRgb(0, 0, 200), Color.FromRgb(200, 0, 0)); break;
                    case ColorMapType.Hot:
                        SetLegend(Colors.Black, Colors.White); break;
                }
            }
        }

        private void SetLegend(Color from, Color to)
        {
            LegendContainer.Background = new LinearGradientBrush(
                from, to, new Point(0, 0), new Point(1, 0));
        }

        // ── Snapshot PNG ──────────────────────────────────────────────────────
        private void BtnSnapshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "KinectSnapshots");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder,
                    $"depth_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    var enc = new PngBitmapEncoder();
                    enc.Frames.Add(BitmapFrame.Create(_bitmap));
                    enc.Save(fs);
                }
                SetStatus($"Guardado → {path}");
            }
            catch (Exception ex) { SetStatus($"Error: {ex.Message}"); }
        }

        // ── Vista previa mesh 3D ──────────────────────────────────────────────
        private void BtnMesh_Click(object sender, RoutedEventArgs e)
        {
            if (_reader.DepthNormalized == null)
            {
                SetStatus("Aún no hay frame capturado.");
                return;
            }

            float[] snapshot = new float[_reader.DepthNormalized.Length];
            Array.Copy(_reader.DepthNormalized, snapshot, snapshot.Length);

            var preview = new MeshPreviewWindow(snapshot);
            preview.Show();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private void SetConnected(bool ok)
        {
            StatusDot.Fill = ok
                ? new SolidColorBrush(Color.FromRgb(0, 200, 80))
                : new SolidColorBrush(Color.FromRgb(255, 64, 64));
        }

        private void SetStatus(string msg) => TxtStatus.Text = msg;

        private void Window_Closing(object sender,
            System.ComponentModel.CancelEventArgs e)
        {
            _cvTracker?.Dispose();
            _reader.FrameReady -= OnFrameReady;
            _reader.Stop();
            _shmDepth.Dispose();
            _udpHand.Dispose();
        }
    }
}