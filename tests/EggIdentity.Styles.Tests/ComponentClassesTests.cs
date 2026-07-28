namespace EggIdentity.Styles.Tests;

public class ComponentClassesTests {
    [Fact]
    public void All_ContainsAtLeastOneKeyFromEachOfTheEightSets() {
        Assert.True(ComponentClasses.All.ContainsKey(".badge"));
        Assert.True(ComponentClasses.All.ContainsKey(".btn-primary"));
        Assert.True(ComponentClasses.All.ContainsKey(".panel"));
        Assert.True(ComponentClasses.All.ContainsKey(".segmented"));
        Assert.True(ComponentClasses.All.ContainsKey(".popover"));
        Assert.True(ComponentClasses.All.ContainsKey(".modal-card"));
        Assert.True(ComponentClasses.All.ContainsKey(".fab-bubble"));
        Assert.True(ComponentClasses.All.ContainsKey(".form-input"));
    }

    [Fact]
    public void All_Count_EqualsSumOfAllEightSetsWithNoOverwrittenKeys() {
        var expected = Components.Badges.Applies.Count
            + Components.Buttons.Applies.Count
            + Components.Panels.Applies.Count
            + Components.SegmentedToggles.Applies.Count
            + Components.Popovers.Applies.Count
            + Components.Modals.Applies.Count
            + Components.FloatingBubbles.Applies.Count
            + Components.FormControls.Applies.Count;

        Assert.Equal(expected, ComponentClasses.All.Count);
    }
}
