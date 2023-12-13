using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DrumGame.Game.Utils;

public static class Statistics
{
    public static RegressionResult LinearRegression(List<double> data)
        => LinearRegression(data.Select((y, x) => ((double)x, y)).ToList());
    public static RegressionResult LinearRegression(IEnumerable<(double x, double y)> data) => LinearRegression(data.ToList());
    public static RegressionResult LinearRegression(List<(double x, double y)> data)
    {
        var n = data.Count;

        var x = data.Select(e => e.x);
        var y = data.Select(e => e.y);

        var xm = x.Average();
        var sx = x.StdDev();

        var ym = y.Average();
        var sy = y.StdDev();

        var r = data.Sum(d => (d.x - xm) / sx * (d.y - ym) / sy) / (n - 1);
        var b1 = r * sy / sx;
        var b0 = ym - b1 * xm;

        var xDiffSq = x.Sum(x => (x - xm) * (x - xm));

        var syx = Math.Sqrt(data.Sum(d =>
        {
            var r = d.y - (d.x * b1 + b0);
            return r * r;
        }) / (n - 2));

        var sb1 = syx / Math.Sqrt(xDiffSq);
        var sb0 = syx * Math.Sqrt(x.Sum(x => x * x) / (n * xDiffSq));

        return new RegressionResult
        {
            Slope = b1,
            SlopeError = sb1,
            Intercept = b0,
            InterceptError = sb0,
            StandardError = syx,
            R = r,
            Count = n
        };
    }

    // sample std dev
    public static double StdDev(this IEnumerable<double> values)
    {
        var n = 0;
        var sum = 0.0;
        var sumSq = 0.0;

        foreach (double x in values)
        {
            n += 1;
            sum += x;
            sumSq += x * x;
        }

        return Math.Sqrt((sumSq - sum * sum / n) / (n - 1));
    }
}

public class RegressionResult
{
    public int Count;
    public double Slope;
    public double SlopeError;
    public double Intercept;
    public double InterceptError;
    public double StandardError;
    public double R;
    public double R2 => R * R;


    public override string ToString() => JsonConvert.SerializeObject(this, Formatting.Indented);
}