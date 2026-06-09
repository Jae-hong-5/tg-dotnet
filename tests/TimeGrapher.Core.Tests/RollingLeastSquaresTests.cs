using TimeGrapher.Core.Metrics;
using Xunit;

namespace TimeGrapher.Core.Tests;

public sealed class RollingLeastSquaresTests
{
    [Fact]
    public void GetRate_FalseWithFewerThanTwoPoints()
    {
        var rls = new RollingLeastSquares(8);
        Assert.False(rls.GetRate(out _));

        rls.AddPoint(0.0, 0.0);
        Assert.False(rls.GetRate(out _));
    }

    [Fact]
    public void GetRate_RecoversSlopeOfLinearData()
    {
        var rls = new RollingLeastSquares(8);
        for (int x = 0; x < 5; x++)
        {
            rls.AddPoint(x, 2.0 * x + 3.0); // slope 2
        }

        Assert.True(rls.GetRate(out double slope));
        Assert.Equal(2.0, slope, 9);
    }

    [Fact]
    public void GetRate_FalseWhenAllXAreEqual()
    {
        var rls = new RollingLeastSquares(8);
        rls.AddPoint(5.0, 1.0);
        rls.AddPoint(5.0, 9.0);

        Assert.False(rls.GetRate(out _)); // singular denominator
    }

    [Fact]
    public void AddPoint_EvictsOldestBeyondCapacity()
    {
        var rls = new RollingLeastSquares(3);
        rls.AddPoint(0.0, 0.0);
        rls.AddPoint(1.0, 1.0);
        rls.AddPoint(2.0, 2.0);   // full window slope would be 1
        rls.AddPoint(3.0, 30.0);
        rls.AddPoint(4.0, 40.0);  // window now {(2,2),(3,30),(4,40)}

        Assert.True(rls.GetRate(out double slope));
        Assert.Equal(19.0, slope, 9); // regression over the retained 3 points, not all 5
    }
}
