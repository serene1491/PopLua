using PopLua.Binding;
using PopLua.Context;
using PopLua.Diagnostics;
using PopLua.Exceptions;
using PopLua.Marshaling;
using PopLua.Runtime;
using PopLua.Sandboxing;

namespace SplitHost.Scripting.Userdata;

/// <summary>Player value exposed to Lua scripts.</summary>
[Userdata("player")]
public partial class Player(string name)
{
    /// <summary>Gets the player name.</summary>
    [Prop("name", ReadOnly = true)]
    public string Name { get; } = name;

    /// <summary>Gets the current score.</summary>
    [Prop("score", ReadOnly = true)]
    public long Score { get; private set; }

    /// <summary>Adds to the player score.</summary>
    /// <param name="amount">Score amount to add.</param>
    /// <returns>The updated score.</returns>
    [Fn("add_score")]
    public long AddScore(long amount)
    {
        Score += amount;
        return Score;
    }
}
