using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace FreePIE.Core.Model
{
    [DataContract]
    public class Curve
    {
        public Curve(List<Point> points) : this(null, points) {}

        public Curve(string name, List<Point> points)
        {
            Name = name;
            Points = points;
            ValidateCurve = true;
        }

        public Curve() {}
        [DataMember]
        public List<Point> Points { get; set; }
        [DataMember]
        public string Name { get; set; }

        public bool? ValidateCurve { get; set; }
        public string selectedcurve { get; set; } = "Linear";
        public int selectedidx { get; set; }

        public int IndexOf(Point point)
        {
            return Points.FindIndex(p => p == point);
        }

        public void Reset(Curve newCurve)
        {
            Points = newCurve.Points;
        }

        public static Curve Create(string name, double yAxisMinValue, double yAxisMaxValue, int pointCount)
        {
            return new Curve(name, CalculateDefault(yAxisMinValue, yAxisMaxValue, pointCount));
        }

        private static List<Point> CalculateDefault(double yAxisMinValue, double yAxisMaxValue, int pointCount)
        {
            var deltaBetweenPoints = (yAxisMaxValue - yAxisMinValue) /(pointCount - 1);
            return Enumerable.Range(0, pointCount)
                       .Select(index => yAxisMinValue + (index * deltaBetweenPoints))
                      .Select(value => new Point(value, value))
                      .ToList();
        }
    }

    public struct Point
    {
        public Point(double x, double y) : this()
        {
            X = x;
            Y = y;
        }

        public double X { get; set; }
        public double Y { get; set; }

        public static bool operator ==(Point x, Point y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            return x.X == y.X && y.Y == y.Y;
        }
        public bool Equals(Point other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return X.Equals(other.X) && Y.Equals(other.Y);
        }
        public override bool Equals(object x)
        {
            if (ReferenceEquals(this, x)) return true;
            if ((object)this == null || x == null) return false;
            return x.GetType() == GetType() && Equals((Point)x);
        }
        public override int GetHashCode() => 0;
        public static bool operator !=(Point x, Point y) => !(x == y);
    }
}
