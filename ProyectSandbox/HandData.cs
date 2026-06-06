using System;

namespace ProyectoSandbox
{
    public enum HandState { Unknown, Open, Closed, Lasso }
    public enum SwipeDirection { None, Left, Right, Up, Down }

    public class HandData
    {
        
        public float X { get; set; }
        public float Y { get; set; }
        // Profundidad en metros
        public float Z { get; set; }

        public bool IsTracked { get; set; }
        public bool IsRight { get; set; }

        public HandState State { get; set; } = HandState.Unknown;
        public SwipeDirection Swipe { get; set; } = SwipeDirection.None;
        public int Fingers { get; set; }
    }
}