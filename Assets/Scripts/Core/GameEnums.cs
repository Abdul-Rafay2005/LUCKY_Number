using System;

namespace LuckyNumber.Core
{
    public enum PlayerRole
    {
        Normal,
        Imposter
    }

    public enum Team
    {
        None,
        NormalTeam,
        ImposterTeam
    }

    public enum GamePhase
    {
        Lobby,
        RoleReveal,
        TurnStart,
        PlayerTurn,
        TurnResolution,
        Discussion,
        Voting,
        Elimination,
        MatchEnded
    }

    public enum TurnActionType
    {
        Add,
        Subtract,
        SkipVoluntary,
        SkipBlocked
    }

    public enum ImposterAbilityType
    {
        ForcedBlock,
        LuckyShift,
        CursedDice
    }

    public enum WinReason
    {
        None,
        TargetReached,
        AllImpostersEliminated,
        OutOfMoves,
        ImposterParity
    }
}

