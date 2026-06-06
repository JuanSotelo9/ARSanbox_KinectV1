using System.Collections.Generic;
using System.Text;

namespace ProyectoSandbox
{
    /// <summary>
    /// Convierte el vector DepthNormalized en una malla 3D lista para exportar.
    /// Se puede llamar desde el evento FrameReady o desde un botón "Capturar mesh".
    /// </summary>
    public static class MeshBuilder
    {
        public const int DefaultStep = 4;

        // Escala del terreno en Unity (unidades del mundo)
        public const float ScaleX = 10f;   // ancho total en unidades Unity
        public const float ScaleZ = 7.5f;  // profundidad total
        public const float ScaleY = 3f;    // altura máxima (depthNorm=1 → 3 unidades)

        /// <summary>
        /// Genera una malla a partir del vector normalizado.
        /// </summary>
        /// <param name="depth">float[307200] de KinectDepthReader, valores 0-1</param>
        /// <param name="srcWidth">Ancho del frame (640)</param>
        /// <param name="srcHeight">Alto del frame (480)</param>
        /// <param name="step">Submuestreo: 1=máximo detalle, 4=recomendado</param>
        public static MeshData Build(float[] depth, int srcWidth, int srcHeight,
                                     int step = DefaultStep)
        {
            // Dimensiones de la grilla resultante
            int cols = srcWidth / step;   // número de columnas de vértices
            int rows = srcHeight / step;   // número de filas de vértices

            var vertices = new List<float[]>(cols * rows);    
            var triangles = new List<int[]>((cols - 1) * (rows - 1) * 2); 

            // ── Generar vértices ──────────────────────────────────────────────
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    // Posición en píxeles del frame original
                    int px = col * step;
                    int py = row * step;
                    int idx = py * srcWidth + px;

                    float depthVal = (idx < depth.Length) ? depth[idx] : 0f;

                    // Convertir a coordenadas 3D en espacio Unity
                    float x = (1f - (col / (float)(cols - 1))) * ScaleX;
                    float z = (1f - (row / (float)(rows - 1))) * ScaleZ;
                    float y = depthVal * ScaleY;   // altura = profundidad normalizada

                    vertices.Add(new float[] { x, y, z });
                }
            }

            // ── Generar triángulos (2 por cada celda del grid) ────────────────
            // Cada celda entre 4 vértices adyacentes se divide en 2 triángulos:
            //   v0──v1
            //   │ ╲ │
            //   v2──v3
            // Tri 1: v0, v1, v2   Tri 2: v1, v3, v2
            for (int row = 0; row < rows - 1; row++)
            {
                for (int col = 0; col < cols - 1; col++)
                {
                    int v0 = row * cols + col;
                    int v1 = row * cols + col + 1;
                    int v2 = (row + 1) * cols + col;
                    int v3 = (row + 1) * cols + col + 1;

                    triangles.Add(new int[] { v0, v1, v2 });
                    triangles.Add(new int[] { v1, v3, v2 });
                }
            }

            return new MeshData
            {
                Vertices = vertices,
                Triangles = triangles,
                Cols = cols,
                Rows = rows
            };
        }
    }

    public class MeshData
    {
        public List<float[]> Vertices;   // posiciones 3D
        public List<int[]> Triangles;  // índices de vértices
        public int Cols, Rows;
    }
}