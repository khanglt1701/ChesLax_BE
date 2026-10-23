namespace Cheslax.Domain;

public enum MatchMode
{
    Casual = 1,
    Ranked = 2,
}

public enum MatchStatus
{
    Waiting = 1,
    Playing = 2,
    Finished = 3,
    Abandoned = 4,
}

public enum MatchmakingTicketStatus
{
    Queued = 1,
    Matched = 2,
    Cancelled = 3,
}

public enum MatchOutcome
{
    Player1Win = 1,
    Player2Win = 2,
    Draw = 3,
}

public enum ResultReason
{
    Checkmate = 1,
    Resignation = 2,
    Timeout = 3,
    Stalemate = 4,
    InsufficientMaterial = 5,
    ThreefoldRepetition = 6,
    FiftyMoveRule = 7,
    AgreedDraw = 8,
    Abandonment = 9,
    OAnQuanScore = 10,
    CannotResupply = 11,
}
