using HueBar.Core;

namespace HueBar.Tests;

public class PairResultTests
{
    [Fact]
    public void Ok_carries_the_username_and_is_not_a_link_button_retry()
    {
        var result = PairResult.Ok("theKey");

        Assert.True(result.Success);
        Assert.Equal("theKey", result.Username);
        Assert.False(result.LinkButtonNotPressed);
    }

    [Fact]
    public void Fail_carries_the_error_type_and_message()
    {
        var result = PairResult.Fail(7, "invalid value");

        Assert.False(result.Success);
        Assert.Null(result.Username);
        Assert.Equal(7, result.ErrorType);
        Assert.Equal("invalid value", result.ErrorMessage);
    }

    [Fact]
    public void LinkButtonNotPressed_is_true_only_for_error_type_101()
    {
        Assert.True(PairResult.Fail(101, "link button not pressed").LinkButtonNotPressed);
        Assert.False(PairResult.Fail(1, "unauthorized").LinkButtonNotPressed);
        Assert.False(PairResult.Ok("k").LinkButtonNotPressed);
    }
}
