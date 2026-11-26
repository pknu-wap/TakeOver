using NUnit.Framework;

public class MoneyTextTests
{
    [TestCase(12000f, "1\uc870 2,000\uc5b5\uc6d0")]
    [TestCase(10048.6768f, "1\uc870 49\uc5b5\uc6d0")]
    [TestCase(0.99999994f, "1\uc5b5\uc6d0")]
    [TestCase(0.9f, "9,000\ub9cc\uc6d0")]
    [TestCase(0.69999999f, "7,000\ub9cc\uc6d0")]
    [TestCase(15.5f, "15\uc5b5 5,000\ub9cc\uc6d0")]
    [TestCase(16f, "16\uc5b5\uc6d0")]
    [TestCase(0.45f, "4,500\ub9cc\uc6d0")]
    [TestCase(0.00025f, "2\ub9cc 5,000\uc6d0")]
    [TestCase(0.0125f, "125\ub9cc\uc6d0")]
    [TestCase(0.00004f, "4,000\uc6d0")]
    [TestCase(0f, "0\uc6d0")]
    [TestCase(-0.45f, "-4,500\ub9cc\uc6d0")]
    public void FormatUsesTwoLargestUnits(float value, string expected)
    {
        Assert.That(MoneyText.Format(value), Is.EqualTo(expected));
    }
}