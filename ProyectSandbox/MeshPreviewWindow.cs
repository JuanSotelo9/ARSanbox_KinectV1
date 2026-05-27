using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ProyectoSandbox
{
    public class MeshPreviewWindow : Window
    {
        // ── Controles ─────────────────────────────────────────────────────────
        private Viewport3D _viewport;
        private ModelVisual3D _meshVisual;
        private PerspectiveCamera _camera;
        private Slider _heightSlider;
        private TextBlock _heightLabel;
        private RadioButton _colorDepth;
        private TextBlock _statusLabel;

        // ── Datos ─────────────────────────────────────────────────────────────
        private float[] _depth;
        private MeshData _mesh;
        private bool _useDepthColor = true;

        // ── Órbita ────────────────────────────────────────────────────────────
        private Point _lastMouse;
        private bool _orbiting;
        private bool _panning;
        private double _yaw = 20;
        private double _pitch = -25;
        private double _radius = 14;
        private Point3D _target = new Point3D(5, 1, 3.75);

        // ─────────────────────────────────────────────────────────────────────
        public MeshPreviewWindow(float[] depthNormalized)
        {
            _depth = depthNormalized;
            Title = "Vista previa mesh 3D — ProyectoSandbox";
            Width = 860;
            Height = 680;
            Background = new SolidColorBrush(Color.FromRgb(13, 13, 13));

            BuildUI();
            Loaded += (s, e) => BuildAndShowMesh();
        }

        // ── Construir UI desde código (sin XAML) ──────────────────────────────
        private void BuildUI()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition
            { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition
            { Height = GridLength.Auto });
            Content = root;

            // ── Viewport 3D ──────────────────────────────────────────────────
            _camera = new PerspectiveCamera { FieldOfView = 50 };
            UpdateCamera();

            _viewport = new Viewport3D { ClipToBounds = true };
            _viewport.Camera = _camera;

            // Luces
            var lights = new ModelVisual3D();
            var lightGroup = new Model3DGroup();
            lightGroup.Children.Add(new AmbientLight(Color.FromRgb(80, 80, 80)));
            lightGroup.Children.Add(new DirectionalLight(
                Colors.White, new Vector3D(0.3, -1, -0.5)));
            lightGroup.Children.Add(new DirectionalLight(
                Color.FromRgb(60, 60, 80), new Vector3D(-0.5, -0.3, 0.8)));
            lights.Content = lightGroup;
            _viewport.Children.Add(lights);

            // Visual de la malla
            _meshVisual = new ModelVisual3D();
            _viewport.Children.Add(_meshVisual);

            Grid.SetRow(_viewport, 0);
            root.Children.Add(_viewport);

            // Eventos ratón
            _viewport.MouseLeftButtonDown += (s, e) =>
            { _orbiting = true; _lastMouse = e.GetPosition(this); };
            _viewport.MouseLeftButtonUp += (s, e) => _orbiting = false;
            _viewport.MouseRightButtonDown += (s, e) =>
            { _panning = true; _lastMouse = e.GetPosition(this); };
            _viewport.MouseRightButtonUp += (s, e) => _panning = false;
            _viewport.MouseMove += OnMouseMove;
            _viewport.MouseWheel += OnMouseWheel;

            // ── Panel de controles ───────────────────────────────────────────
            var panel = new StackPanel
            {
                Background = new SolidColorBrush(Color.FromRgb(17, 17, 17)),
                Orientation = Orientation.Vertical
            };

            // Padding manual con un borde
            var panelBorder = new Border
            {
                Child = panel,
                Padding = new Thickness(12, 8, 12, 8)
            };

            panel.Children.Add(MakeLbl("CONTROLES", "#333333", 9));

            var hintRow = new StackPanel { Orientation = Orientation.Horizontal };
            hintRow.Children.Add(MakeLbl("Rotar: botón izq + arrastrar   ", "#555555", 11));
            hintRow.Children.Add(MakeLbl("Zoom: rueda   ", "#555555", 11));
            hintRow.Children.Add(MakeLbl("Pan: botón der + arrastrar", "#555555", 11));
            panel.Children.Add(hintRow);

            // Fila de sliders y botones
            var ctrlRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 8, 0, 0)
            };

            ctrlRow.Children.Add(MakeLbl("Altura:  ", "#555555", 11));

            _heightSlider = new Slider
            {
                Minimum = 0.5,
                Maximum = 8,
                Value = 3,
                Width = 160,
                VerticalAlignment = VerticalAlignment.Center
            };
            _heightSlider.ValueChanged += HeightSlider_Changed;
            ctrlRow.Children.Add(_heightSlider);

            _heightLabel = MakeLbl("  3.0x  ", "#00E5FF", 11);
            ctrlRow.Children.Add(_heightLabel);

            ctrlRow.Children.Add(MakeLbl("  Color:  ", "#555555", 11));

            _colorDepth = new RadioButton
            {
                Content = "Terreno",
                IsChecked = true,
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _colorDepth.Checked += ColorMode_Changed;
            ctrlRow.Children.Add(_colorDepth);

            var colorGray = new RadioButton
            {
                Content = "Gris",
                Foreground = new SolidColorBrush(Color.FromRgb(136, 136, 136)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            colorGray.Checked += ColorMode_Changed;
            ctrlRow.Children.Add(colorGray);

            // Botón exportar OBJ
            var btnExport = new Button
            {
                Content = "  Guardar .OBJ  ",
                Margin = new Thickness(20, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 24)),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 42)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            btnExport.Click += BtnExport_Click;
            ctrlRow.Children.Add(btnExport);

            panel.Children.Add(ctrlRow);

            // Status
            _statusLabel = MakeLbl("Generando malla...", "#444444", 10);
            _statusLabel.Margin = new Thickness(0, 6, 0, 0);
            panel.Children.Add(_statusLabel);

            Grid.SetRow(panelBorder, 1);
            root.Children.Add(panelBorder);
        }

        private TextBlock MakeLbl(string text, string hex, double size)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return new TextBlock
            {
                Text = text,
                Foreground = new SolidColorBrush(c),
                FontFamily = new FontFamily("Consolas"),
                FontSize = size,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 2)
            };
        }

        // ── Construir malla inicial ───────────────────────────────────────────
        private void BuildAndShowMesh()
        {
            _mesh = MeshBuilder.Build(_depth, 640, 480, MeshBuilder.DefaultStep);
            RefreshMesh();
            _statusLabel.Text =
                $"Malla lista: {_mesh.Vertices.Count} vértices · " +
                $"{_mesh.Triangles.Count} triángulos";
        }

        // ── Regenerar geometría con parámetros actuales ───────────────────────
        private void RefreshMesh()
        {
            if (_mesh == null) return;
            double hScale = _heightSlider?.Value ?? 3.0;
            _meshVisual.Content = BuildColoredMesh(_mesh, hScale, _useDepthColor);
        }

        // ── Malla coloreada por bandas de altura ──────────────────────────────
        private Model3DGroup BuildColoredMesh(MeshData data,
            double heightScale, bool depthColor)
        {
            var group = new Model3DGroup();
            double hs = heightScale / MeshBuilder.ScaleY;
            int bands = depthColor ? 12 : 1;

            for (int band = 0; band < bands; band++)
            {
                float bandMin = (float)band / bands;
                float bandMax = (float)(band + 1) / bands;

                var geo = new MeshGeometry3D();
                var indexMap = new Dictionary<int, int>();

                foreach (var t in data.Triangles)
                {
                    float avgH = (data.Vertices[t[0]][1] +
                                  data.Vertices[t[1]][1] +
                                  data.Vertices[t[2]][1]) / 3f;

                    if (avgH < bandMin || avgH >= bandMax) continue;

                    int[] localIdx = new int[3];
                    for (int k = 0; k < 3; k++)
                    {
                        int orig = t[k];
                        if (!indexMap.TryGetValue(orig, out int li))
                        {
                            var vv = data.Vertices[orig];
                            li = geo.Positions.Count;
                            geo.Positions.Add(new Point3D(vv[0], vv[1] * hs, vv[2]));
                            indexMap[orig] = li;
                        }
                        localIdx[k] = li;
                    }

                    geo.TriangleIndices.Add(localIdx[0]);
                    geo.TriangleIndices.Add(localIdx[1]);
                    geo.TriangleIndices.Add(localIdx[2]);
                }

                if (geo.Positions.Count == 0) continue;

                Color c = depthColor
                    ? BandColor(band, bands)
                    : Color.FromRgb(180, 180, 180);

                // DiffuseMaterial  → sombras de la luz (da forma 3D)
                // EmissiveMaterial → color base vivo sin depender de la luz
                var mat = new MaterialGroup();
                mat.Children.Add(new DiffuseMaterial(new SolidColorBrush(
                    Color.FromRgb(
                        (byte)(c.R * 0.45),
                        (byte)(c.G * 0.45),
                        (byte)(c.B * 0.45)))));
                mat.Children.Add(new EmissiveMaterial(new SolidColorBrush(
                    Color.FromRgb(
                        (byte)(c.R * 0.75),
                        (byte)(c.G * 0.75),
                        (byte)(c.B * 0.75)))));

                group.Children.Add(new GeometryModel3D
                {
                    Geometry = geo,
                    Material = mat,
                    BackMaterial = new DiffuseMaterial(
                        new SolidColorBrush(Color.FromRgb(20, 20, 20)))
                });
            }

            return group;
        }

        // Gradiente terrain: azul → verde → amarillo → rojo
        private Color BandColor(int band, int totalBands)
        {
            float t = (float)band / Math.Max(totalBands - 1, 1);

            Color[] anchors =
            {
                Color.FromRgb(0,   40,  220),  // azul   (lejos / bajo)
                Color.FromRgb(0,   200,  80),  // verde
                Color.FromRgb(230, 200,   0),  // amarillo
                Color.FromRgb(220,  30,  20),  // rojo   (cerca / alto)
            };

            float scaled = t * (anchors.Length - 1);
            int lo = (int)scaled;
            int hi = Math.Min(lo + 1, anchors.Length - 1);
            float frac = scaled - lo;

            var a = anchors[lo];
            var b = anchors[hi];
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * frac),
                (byte)(a.G + (b.G - a.G) * frac),
                (byte)(a.B + (b.B - a.B) * frac));
        }

        // ── Eventos de controles ──────────────────────────────────────────────
        private void HeightSlider_Changed(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (_heightLabel != null)
                _heightLabel.Text = $"  {e.NewValue:F1}x  ";
            RefreshMesh();
        }

        private void ColorMode_Changed(object sender, RoutedEventArgs e)
        {
            _useDepthColor = _colorDepth?.IsChecked == true;
            RefreshMesh();
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            if (_mesh == null) return;
            try
            {
                string folder = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "KinectMeshes");
                System.IO.Directory.CreateDirectory(folder);
                string path = System.IO.Path.Combine(folder,
                    $"terrain_{DateTime.Now:yyyyMMdd_HHmmss}.obj");
                ObjExporter.Save(_mesh, path);
                _statusLabel.Text = $"Guardado → {path}";
            }
            catch (Exception ex)
            {
                _statusLabel.Text = $"Error: {ex.Message}";
            }
        }

        // ── Órbita de cámara ──────────────────────────────────────────────────
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            var delta = pos - _lastMouse;
            _lastMouse = pos;

            if (_orbiting)
            {
                _yaw += delta.X * 0.5;
                _pitch = Math.Max(-80, Math.Min(80, _pitch - delta.Y * 0.5));
                UpdateCamera();
            }
            else if (_panning)
            {
                double scale = _radius * 0.002;
                _target.X -= delta.X * scale;
                _target.Y += delta.Y * scale;
                UpdateCamera();
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _radius = Math.Max(2, Math.Min(40, _radius - e.Delta * 0.01));
            UpdateCamera();
        }

        private void UpdateCamera()
        {
            double yr = _yaw * Math.PI / 180.0;
            double pr = _pitch * Math.PI / 180.0;

            double cx = _target.X + _radius * Math.Sin(yr) * Math.Cos(pr);
            double cy = _target.Y + _radius * Math.Sin(pr);
            double cz = _target.Z + _radius * Math.Cos(yr) * Math.Cos(pr);

            _camera.Position = new Point3D(cx, cy, cz);
            _camera.LookDirection = new Vector3D(
                _target.X - cx, _target.Y - cy, _target.Z - cz);
        }
    }
}