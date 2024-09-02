using NUnit.Framework;
using ServiceControl.TransportAdapter;

[TestFixture]
public class AddressSanitizerTests
{
    [Test]
    public void It_accepts_single_part()
    {
        var result = AddressSanitizer.MakeV5CompatibleAddress("Queue");

        Assert.That(result, Is.EqualTo("Queue"));
    }

    [Test]
    public void It_preserves_two_parts()
    {
        var result = AddressSanitizer.MakeV5CompatibleAddress("Queue@Machine");

        Assert.That(result, Is.EqualTo("Queue@Machine"));
    }

    [Test]
    public void It_ignores_third_part_if_present()
    {
        var result = AddressSanitizer.MakeV5CompatibleAddress("Queue@Machine@Something");

        Assert.That(result, Is.EqualTo("Queue@Machine"));
    }
}
