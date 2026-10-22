using Cheslax.Domain;

namespace Cheslax.GameEngine;

/// <summary>
/// Marker for game-specific engines. Concrete engines live in this assembly and must be
/// deterministic, free of I/O, and independent of ASP.NET / React.
/// </summary>
public interface IGameEngine
{
    GameType GameType { get; }
}
