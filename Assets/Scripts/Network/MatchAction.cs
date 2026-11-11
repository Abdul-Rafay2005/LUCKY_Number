using System;
using Unity.Collections;
using Unity.Netcode;
using LuckyNumber.Core;

namespace LuckyNumber.Network
{
    public enum MatchAction
    {
        Start,
        Rematch,
        Move,
        Skip,
        ForcedBlock,
        LuckyShift,
        CursedDice,
        Vote
    }

    public struct ActionStamp : INetworkSerializeByMemcpy, IEquatable<ActionStamp>
    {
        public ulong SessionId;
        public uint MatchId;
        public uint ActionId;

        public bool Equals(ActionStamp other) =>
            SessionId == other.SessionId && MatchId == other.MatchId && ActionId == other.ActionId;
    }

    public struct PrivateRoll : INetworkSerializeByMemcpy, IEquatable<PrivateRoll>
    {
        public ActionStamp Stamp;
        public int Die1;
        public int Die2;
        public int Die3;
        public bool IsCursed;
        public bool HasDice => Die1 > 0;

        public bool Equals(PrivateRoll other) => Stamp.Equals(other.Stamp) && Die1 == other.Die1 &&
            Die2 == other.Die2 && Die3 == other.Die3 && IsCursed == other.IsCursed;
    }

    internal struct PlayerSecret : INetworkSerializeByMemcpy, IEquatable<PlayerSecret>
    {
        public bool HasRole;
        public PlayerRole Role;
        public int BlockCharges;
        public int ShiftCharges;
        public int CurseCharges;
        public int LastAbilityRound;
        public FixedList128Bytes<ulong> KnownImposters;

        public bool Equals(PlayerSecret other) =>
            HasRole == other.HasRole && Role == other.Role && BlockCharges == other.BlockCharges &&
            ShiftCharges == other.ShiftCharges && CurseCharges == other.CurseCharges && LastAbilityRound == other.LastAbilityRound &&
            KnownImposters.Equals(other.KnownImposters);
    }
}
