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

    public enum PublicEventKind : byte
    {
        NumberMove,
        Skip,
        TargetShift
    }

    public struct PublicGameEvent : INetworkSerializable, IEquatable<PublicGameEvent>
    {
        public uint Sequence;
        public int Round;
        public ulong ActorClientId;
        public FixedString64Bytes ActorName;
        public PublicEventKind Kind;
        public int SignedValue;
        public int ResultingScore;
        public int ResultingTarget;
        public int RemainingMoves;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Sequence);
            serializer.SerializeValue(ref Round);
            serializer.SerializeValue(ref ActorClientId);
            serializer.SerializeValue(ref ActorName);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref SignedValue);
            serializer.SerializeValue(ref ResultingScore);
            serializer.SerializeValue(ref ResultingTarget);
            serializer.SerializeValue(ref RemainingMoves);
        }

        public bool Equals(PublicGameEvent other) => Sequence == other.Sequence && Round == other.Round &&
            ActorClientId == other.ActorClientId && ActorName.Equals(other.ActorName) && Kind == other.Kind &&
            SignedValue == other.SignedValue && ResultingScore == other.ResultingScore &&
            ResultingTarget == other.ResultingTarget && RemainingMoves == other.RemainingMoves;
    }

    public enum VoteOutcomeKind : byte
    {
        None,
        NoVotes,
        SkipEjection,
        Tie,
        RunoffStarted,
        Ejection,
        RandomEjection,
        DisconnectCancellation
    }

    public struct VoteOutcomeSnapshot : INetworkSerializable, IEquatable<VoteOutcomeSnapshot>
    {
        public VoteOutcomeKind Kind;
        public int RunoffBallot;
        public ActionStamp Stamp;
        public ulong PlayerId;
        public FixedString64Bytes PlayerName;
        public PlayerRole PlayerRole;
        public FixedString128Bytes Message;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref RunoffBallot);
            serializer.SerializeValue(ref Stamp);
            serializer.SerializeValue(ref PlayerId);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref PlayerRole);
            serializer.SerializeValue(ref Message);
        }

        public bool Equals(VoteOutcomeSnapshot other) => Kind == other.Kind && RunoffBallot == other.RunoffBallot && Stamp.Equals(other.Stamp) &&
            PlayerId == other.PlayerId && PlayerName.Equals(other.PlayerName) && PlayerRole == other.PlayerRole &&
            Message.Equals(other.Message);
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
        public uint MatchId;
        public PlayerRole Role;
        public int BlockCharges;
        public int ShiftCharges;
        public int CurseCharges;
        public int LastAbilityRound;
        public int AbilityUsesThisRound;
        public FixedList128Bytes<ulong> KnownImposters;

        public bool Equals(PlayerSecret other) =>
            HasRole == other.HasRole && MatchId == other.MatchId && Role == other.Role && BlockCharges == other.BlockCharges &&
            ShiftCharges == other.ShiftCharges && CurseCharges == other.CurseCharges && LastAbilityRound == other.LastAbilityRound &&
            AbilityUsesThisRound == other.AbilityUsesThisRound &&
            KnownImposters.Equals(other.KnownImposters);
    }
}
