using System;
using System.Linq;
using Microsoft.Kinect;
namespace ProyectoSandbox
{
    public class KinectDepthReader
    {
        private KinectSensor _sensor;
        public KinectSensor Sensor => _sensor;

        public float[] DepthNormalized { get; private set; }
        public event EventHandler<float[]> FrameReady;
        public event EventHandler<short[]> RawFrameReady;


        // Rango de profundidad (en mm)
        public int MinDepthMm { get; set; } = 1000;
        public int MaxDepthMm { get; set; } = 1250;

        // 🎯 RECORTE (ajústalo según tu caja física)
        public int CropXStart = 50;
        public int CropXEnd = 590;

        public int CropYStart = 30;
        public int CropYEnd = 420;

        private int _width;
        private int _height;

        public void Start()
        {
            _sensor = KinectSensor.KinectSensors
                          .FirstOrDefault(s => s.Status == KinectStatus.Connected);

            if (_sensor == null)
                throw new Exception("No se encontró ningún Kinect conectado.");

            // Habilitar streams necesarios: Depth y Skeleton (necesario para HandTracker)
            _sensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

            // <-- Se añadió esta línea para que lleguen SkeletonFrameReady events
            _sensor.SkeletonStream.Enable();

            _sensor.DepthFrameReady += OnDepthFrameReady;
            _sensor.Start();
            

            _width = _sensor.DepthStream.FrameWidth;
            _height = _sensor.DepthStream.FrameHeight;

            DepthNormalized = new float[_width * _height];

            Console.WriteLine($"Kinect iniciado: {_width}x{_height}");
            Console.WriteLine($"Rango: {MinDepthMm}mm – {MaxDepthMm}mm");
            Console.WriteLine($"Crop X: {CropXStart}-{CropXEnd}");
            Console.WriteLine($"Crop Y: {CropYStart}-{CropYEnd}");
        }

        private void OnDepthFrameReady(object sender, DepthImageFrameReadyEventArgs e)
        {
            using (DepthImageFrame frame = e.OpenDepthImageFrame())
            {
                if (frame == null) return;

                short[] rawData = new short[frame.PixelDataLength];
                frame.CopyPixelDataTo(rawData);
                RawFrameReady?.Invoke(this, rawData);

                for (int i = 0; i < rawData.Length; i++)
                {
                    int x = i % _width;
                    int y = i / _width;

                    // ✂️ FILTRO DE RECORTE (ROI)
                    if (x < CropXStart || x > CropXEnd || y < CropYStart || y > CropYEnd)
                    {
                        DepthNormalized[i] = 0f;
                        continue;
                    }

                    int depthMm = rawData[i] >> DepthImageFrame.PlayerIndexBitmaskWidth;

                    if (depthMm <= 0)
                    {
                        DepthNormalized[i] = 0f;
                    }
                    else
                    {
                           
                        // Clamp al rango definido
                        float clamped = Math.Max(MinDepthMm, Math.Min(MaxDepthMm, depthMm));

                        // 🔥 NORMALIZAR + INVERTIR (CLAVE)
                        float normalized = (clamped - MinDepthMm)
                                         / (float)(MaxDepthMm - MinDepthMm);

                        DepthNormalized[i] = 1f - normalized;
                    }
                }

                FrameReady?.Invoke(this, DepthNormalized);
            }
        }

        public void Stop()
        {
            if (_sensor != null && _sensor.IsRunning)
            {
                _sensor.DepthFrameReady -= OnDepthFrameReady;
                _sensor.Stop();
                _sensor.Dispose();
            }
        }

        // Exponer el sensor para que HandTracker se suscriba al mismo dispositivo
        
    }
}