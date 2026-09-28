using TabletAb.Core.Protocol;

namespace TabletAb.Core.Tests;

public class ProtocolCatalogTests
{
    [Fact]
    public void PacingModes_HasThreeEntriesWithValuesZeroThroughTwo()
    {
        Assert.Equal(3, ProtocolCatalog.PacingModes.Length);
        Assert.Equal(new[] { 0, 1, 2 }, ProtocolCatalog.PacingModes.Select(m => m.Value));
    }
}
