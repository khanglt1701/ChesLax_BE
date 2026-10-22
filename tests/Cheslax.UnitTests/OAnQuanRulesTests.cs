using Cheslax.GameEngine.OAnQuan;

namespace Cheslax.UnitTests;

public class OAnQuanRulesTests
{
    [Fact]
    public void Initial_board_has_five_dan_and_one_quan()
    {
        var state = OAnQuanRules.CreateInitialState();
        Assert.Equal(12, state.Pits.Count);
        Assert.Equal(10, state.Pits.Count(pit => !pit.IsQuan));
        Assert.Equal(2, state.Pits.Count(pit => pit.IsQuan));
        foreach (var pit in state.Pits)
        {
            if (pit.IsQuan)
            {
                Assert.Equal(0, pit.Dan);
                Assert.Equal(1, pit.Quan);
            }
            else
            {
                Assert.Equal(5, pit.Dan);
                Assert.Equal(0, pit.Quan);
            }
        }

        Assert.Equal(0, state.CurrentPlayer);
        Assert.Equal("playing", state.Status);
    }

    [Fact]
    public void Only_current_player_occupied_dan_pits_are_legal()
    {
        var state = OAnQuanRules.CreateInitialState();
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, OAnQuanRules.GetValidPits(state));
        Assert.True(OAnQuanRules.IsValidMove(state, 0));
        Assert.False(OAnQuanRules.IsValidMove(state, 6));
        Assert.False(OAnQuanRules.IsValidMove(state, OAnQuanState.LeftQuanIndex));
    }

    [Fact]
    public void Sows_clockwise_including_quan_pits()
    {
        var state = WithOpponentMove(EmptyDanPits());
        state.Pits[0].Dan = 3;
        state.Pits[OAnQuanState.RightQuanIndex].Quan = 0;
        var result = OAnQuanRules.ApplyMove(state, 0, OAnQuanDirection.Clockwise);
        Assert.Equal(0, result.State.Pits[0].Dan);
        Assert.Equal(1, result.State.Pits[1].Dan);
        Assert.Equal(1, result.State.Pits[2].Dan);
        Assert.Equal(1, result.State.Pits[3].Dan);
        Assert.Equal(3, result.Events.OfType<DropEvent>().Count());
        Assert.Equal(1, result.State.CurrentPlayer);
    }

    [Fact]
    public void Relays_when_next_dan_pit_has_tokens()
    {
        var state = WithOpponentMove(EmptyDanPits());
        state.Pits[0].Dan = 1;
        state.Pits[2].Dan = 2;
        var result = OAnQuanRules.ApplyMove(state, 0, OAnQuanDirection.Clockwise);
        Assert.Contains(result.Events, e => e is RelayEvent relay && relay.Pit == 2);
        Assert.Equal(1, result.State.Pits[1].Dan);
        Assert.Equal(0, result.State.Pits[2].Dan);
        Assert.Equal(1, result.State.Pits[3].Dan);
        Assert.Equal(1, result.State.Pits[4].Dan);
    }

    [Fact]
    public void Does_not_relay_from_occupied_quan()
    {
        var state = EmptyDanPits();
        state.Pits[4].Dan = 1;
        state.Pits[OAnQuanState.RightQuanIndex].Dan = 4;
        state.Pits[7].Dan = 1;
        var result = OAnQuanRules.ApplyMove(state, 4, OAnQuanDirection.Clockwise);
        Assert.DoesNotContain(result.Events, e => e is RelayEvent);
        Assert.Equal(5, result.State.Pits[OAnQuanState.RightQuanIndex].Dan);
        Assert.Equal(1, result.State.Pits[OAnQuanState.RightQuanIndex].Quan);
    }

    [Fact]
    public void Captures_the_pit_after_an_empty_pit()
    {
        var state = WithOpponentMove(EmptyDanPits());
        state.Pits[0].Dan = 1;
        state.Pits[3].Dan = 4;
        state.Pits[4].Dan = 1;
        var result = OAnQuanRules.ApplyMove(state, 0, OAnQuanDirection.Clockwise);
        Assert.Contains(result.Events, e => e is CaptureEvent capture && capture.Pit == 3);
        Assert.Equal(0, result.State.Pits[3].Dan);
        Assert.Equal(4, result.State.Captured[0].Dan);
    }

    [Fact]
    public void Chains_captures_including_quan()
    {
        var state = WithOpponentMove(EmptyDanPits());
        state.Pits[0].Dan = 1;
        state.Pits[3].Dan = 3;
        var result = OAnQuanRules.ApplyMove(state, 0, OAnQuanDirection.Clockwise);
        var captures = result.Events.OfType<CaptureEvent>().Select(e => e.Pit).ToArray();
        Assert.Equal(new[] { 3, OAnQuanState.RightQuanIndex }, captures);
        Assert.Equal(3, result.State.Captured[0].Dan);
        Assert.Equal(1, result.State.Captured[0].Quan);
    }

    [Fact]
    public void Resupplies_from_captured_dan()
    {
        var state = EmptyDanPits(1);
        state.Captured[1].Dan = 8;
        var (next, events) = OAnQuanRules.ApplyResupplyIfNeeded(state);
        Assert.Contains(events, e => e is ResupplyEvent);
        Assert.Equal(3, next.Captured[1].Dan);
        Assert.All(next.Pits.Skip(6).Take(5), pit => Assert.Equal(1, pit.Dan));
    }

    [Fact]
    public void Ends_when_player_cannot_resupply()
    {
        var state = EmptyDanPits(0);
        state.Captured[0].Dan = 2;
        var (next, _) = OAnQuanRules.ApplyResupplyIfNeeded(state);
        Assert.Equal("ended", next.Status);
        Assert.Equal("1", next.Winner);
        Assert.Equal(OAnQuanEndReason.CannotResupply, next.EndReason);
    }

    [Fact]
    public void Ends_when_both_quan_pits_empty_and_scores_remaining_dan()
    {
        var state = EmptyDanPits();
        state.Pits[OAnQuanState.LeftQuanIndex].Quan = 0;
        state.Pits[2].Dan = 1;
        state.Pits[0].Dan = 4;
        state.Pits[8].Dan = 6;
        var result = OAnQuanRules.ApplyMove(state, 2, OAnQuanDirection.Clockwise);
        Assert.True(OAnQuanRules.BothQuanPitsEmpty(result.State));
        Assert.Equal("ended", result.State.Status);
        Assert.Equal(OAnQuanEndReason.BothQuanEmpty, result.State.EndReason);
        Assert.Equal(5, result.State.Captured[0].Dan);
        Assert.Equal(1, result.State.Captured[0].Quan);
        Assert.Equal(6, result.State.Captured[1].Dan);
        Assert.Equal("0", result.State.Winner);
    }

    [Fact]
    public void Rejects_out_of_turn_moves()
    {
        var state = OAnQuanRules.CreateInitialState();
        Assert.Throws<InvalidOperationException>(() =>
            OAnQuanRules.ApplyMoveForPlayer(state, 0, OAnQuanDirection.Clockwise, 1));
    }

    [Fact]
    public void Wraps_around_the_board()
    {
        Assert.Equal(0, OAnQuanRules.NextIndex(11, OAnQuanDirection.Clockwise));
        Assert.Equal(11, OAnQuanRules.NextIndex(0, OAnQuanDirection.CounterClockwise));
    }

    private static OAnQuanState EmptyDanPits(int currentPlayer = 0)
    {
        var state = OAnQuanRules.CreateInitialState();
        foreach (var pit in state.Pits.Where(pit => !pit.IsQuan))
        {
            pit.Dan = 0;
            pit.Quan = 0;
        }

        state.CurrentPlayer = currentPlayer;
        return state;
    }

    private static OAnQuanState WithOpponentMove(OAnQuanState state)
    {
        state.Pits[6].Dan = Math.Max(state.Pits[6].Dan, 1);
        return state;
    }
}
