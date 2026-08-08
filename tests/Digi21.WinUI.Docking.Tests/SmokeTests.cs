using Xunit;

namespace Digi21.WinUI.Docking.Tests;

public class SmokeTests
{
    [Fact]
    public void LibraryAssemblyLoads()
    {
        // Ensures the library assembly can be loaded by the test host without a XAML runtime.
        var assembly = typeof(DockSite).Assembly;

        Assert.Equal("Digi21.WinUI.Docking", assembly.GetName().Name);
    }
}
