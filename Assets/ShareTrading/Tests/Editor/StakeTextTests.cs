using NUnit.Framework;

public class StakeTextTests
{
    [TestCase(0d, "0%")]
    [TestCase(0.000025d, "0.001%")]
    [TestCase(0.001499d, "0.001%")]
    [TestCase(0.0015d, "0.002%")]
    [TestCase(12.3456d, "12.346%")]
    [TestCase(12.34d, "12.34%")]
    [TestCase(20d, "20%")]
    [TestCase(99.9996d, "100%")]
    public void FormatRoundsAndPreservesVisibleOwnership(double percent, string expected) =>
        Assert.That(StakeText.Format(percent), Is.EqualTo(expected));

    [TestCase(0, 4000000, "0%")]
    [TestCase(1, 4000000, "0.001%")]
    [TestCase(1, 0, "0%")]
    public void FormatSharesUsesTheShareCount(int owned, int total, string expected) =>
        Assert.That(StakeText.FormatShares(owned, total), Is.EqualTo(expected));

    [TestCase("12.00 % → 10.50%", "12% → 10.5%")]
    [TestCase("0.00%", "0%")]
    [TestCase("조회 불가", "조회 불가")]
    public void FormatSavedPreservesHistoricalValues(string saved, string expected) =>
        Assert.That(StakeText.FormatSaved(saved), Is.EqualTo(expected));
}
