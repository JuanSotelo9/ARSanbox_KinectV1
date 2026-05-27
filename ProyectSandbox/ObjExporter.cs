using System.IO;
using System.Text;

namespace ProyectoSandbox
{
    /// <summary>
    /// Exporta un MeshData al formato Wavefront .OBJ.
    /// Unity acepta este formato sin plugins adicionales.
    /// </summary>
    public static class ObjExporter
    {
        /// <summary>
        /// Guarda la malla como archivo .obj.
        /// </summary>
        /// <param name="mesh">Datos generados por MeshBuilder.Build()</param>
        /// <param name="filePath">Ruta completa, ej: C:\...\terrain.obj</param>
        public static void Save(MeshData mesh, string filePath)
        {
            var sb = new StringBuilder();

            // Cabecera
            sb.AppendLine("# Kinect depth terrain — ProyectoSandbox");
            sb.AppendLine($"# Vertices: {mesh.Vertices.Count}");
            sb.AppendLine($"# Triangles: {mesh.Triangles.Count}");
            sb.AppendLine("o KinectTerrain");
            sb.AppendLine();

            // Vértices: "v X Y Z"
            foreach (var v in mesh.Vertices)
            {
                // OBJ usa punto como separador decimal, independiente del locale
                sb.AppendLine(
                    $"v {v[0].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}" +
                    $" {v[1].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}" +
                    $" {v[2].ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}");
            }

            sb.AppendLine();

            // Normales simples apuntando hacia arriba (Unity las puede recalcular)
            sb.AppendLine("vn 0.0000 1.0000 0.0000");
            sb.AppendLine();

            // Caras: "f v1//vn v2//vn v3//vn"
            // OBJ usa índices base-1 (no base-0)
            foreach (var t in mesh.Triangles)
            {
                sb.AppendLine($"f {t[0] + 1}//1 {t[1] + 1}//1 {t[2] + 1}//1");
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }
    }
}