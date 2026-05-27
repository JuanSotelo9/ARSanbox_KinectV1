using System;

namespace ProyectoSandbox
{
    public enum ColorMapType { Grayscale, Inverse, Jet, Hot }

    /// <summary>
    /// Convierte el vector DepthNormalized float[0-1] en bytes BGRA
    /// para el WriteableBitmap. Opera directo en buffer → sin allocations.
    /// </summary>
    public static class DepthColorMapper
    {
        public static void WriteToBuffer(float[] depth, byte[] bgraBuffer, ColorMapType map)
        {
            for (int i = 0; i < depth.Length; i++)
            {
                float v = depth[i];
                int o = i * 4; // offset en buffer BGRA

                // Píxel sin datos → magenta oscuro distintivo
                if (v <= 0f)
                {
                    bgraBuffer[o]     = 60;
                    bgraBuffer[o + 1] = 0;
                    bgraBuffer[o + 2] = 60;
                    bgraBuffer[o + 3] = 255;
                    continue;
                }

                if (v > 1f) v = 1f;

                byte r, g, b;
                switch (map)
                {
                    case ColorMapType.Grayscale:
                        r = g = b = (byte)(v * 255f);
                        break;
                    case ColorMapType.Inverse:
                        r = g = b = (byte)((1f - v) * 255f);
                        break;
                    case ColorMapType.Jet:
                        JetColor(v, out r, out g, out b);
                        break;
                    case ColorMapType.Hot:
                        HotColor(v, out r, out g, out b);
                        break;
                    default:
                        r = g = b = (byte)(v * 255f);
                        break;
                }

                bgraBuffer[o]     = b;
                bgraBuffer[o + 1] = g;
                bgraBuffer[o + 2] = r;
                bgraBuffer[o + 3] = 255;
            }
        }

        static void JetColor(float v, out byte r, out byte g, out byte b)
        {
            float x = v * 4f;
            b = (byte)(Clamp01(1.5f - Math.Abs(x - 3f)) * 255f);
            g = (byte)(Clamp01(1.5f - Math.Abs(x - 2f)) * 255f);
            r = (byte)(Clamp01(1.5f - Math.Abs(x - 1f)) * 255f);
        }

        static void HotColor(float v, out byte r, out byte g, out byte b)
        {
            r = (byte)(Clamp01(v * 3f)        * 255f);
            g = (byte)(Clamp01(v * 3f - 1f)   * 255f);
            b = (byte)(Clamp01(v * 3f - 2f)   * 255f);
        }

        static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
    }
}
