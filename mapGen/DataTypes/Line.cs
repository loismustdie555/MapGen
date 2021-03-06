﻿namespace MapGen 
{
    public class Line
    {
        public UnityEngine.Vector2 p0 { get; private set; }
        public UnityEngine.Vector2 p1 { get; private set; }

        public Line(UnityEngine.Vector2? p0, UnityEngine.Vector2? p1)
        {
            this.p0 = p0.Value;
            this.p1 = p1.Value;
        }
    }
}