namespace EverythingButTheSink.CompatibilityFixture.Tests;

internal sealed class CompatibilityTests
{
    [Test]
    public async Task Should_execute_synthetic_test()
    {
        await Assert.That(2 + 2).IsEqualTo(4);
    }
}
