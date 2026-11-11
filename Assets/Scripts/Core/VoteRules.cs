using System.Collections.Generic;
using System.Linq;

namespace LuckyNumber.Core
{
    public enum VoteResolutionKind
    {
        NoVotes,
        SkipEjection,
        TiedCandidates,
        Eliminate
    }

    public sealed class VoteResolution
    {
        public VoteResolutionKind Kind { get; }
        public int VotesAtLead { get; }
        public IReadOnlyList<ulong> LeadingCandidates { get; }

        internal VoteResolution(VoteResolutionKind kind, int votesAtLead, List<ulong> leadingCandidates)
        {
            Kind = kind;
            VotesAtLead = votesAtLead;
            LeadingCandidates = leadingCandidates;
        }
    }

    public static class VoteRules
    {
        public static VoteResolution Tally(IReadOnlyDictionary<ulong, ulong> suspectVotes,
            IReadOnlyCollection<ulong> skipVoters, IEnumerable<ulong> eligibleCandidates,
            IEnumerable<ulong> eligibleVoters)
        {
            var eligible = new HashSet<ulong>(eligibleCandidates);
            var voters = new HashSet<ulong>(eligibleVoters);
            var counts = new Dictionary<ulong, int>();
            foreach (var ballot in suspectVotes)
            {
                if (!voters.Contains(ballot.Key) || !eligible.Contains(ballot.Value)) continue;
                counts[ballot.Value] = counts.TryGetValue(ballot.Value, out int count) ? count + 1 : 1;
            }

            int skips = skipVoters.Count(voters.Contains);
            int suspectLead = counts.Count == 0 ? 0 : counts.Values.Max();
            int lead = System.Math.Max(skips, suspectLead);
            if (lead == 0)
                return new VoteResolution(VoteResolutionKind.NoVotes, 0, new List<ulong>());
            if (skips >= suspectLead)
                return new VoteResolution(VoteResolutionKind.SkipEjection, lead, new List<ulong>());

            var leaders = counts.Where(pair => pair.Value == lead).Select(pair => pair.Key).OrderBy(id => id).ToList();
            return leaders.Count > 1
                ? new VoteResolution(VoteResolutionKind.TiedCandidates, lead, leaders)
                : new VoteResolution(VoteResolutionKind.Eliminate, lead, leaders);
        }
    }

    public enum TieFollowup
    {
        StartRunoff,
        RandomEjection,
        NoEjection
    }

    public sealed class VoteRunoffTracker
    {
        private string lastTieKey;
        private int identicalTieCount;

        public TieFollowup RegisterTie(IReadOnlyList<ulong> tiedCandidates, int runoffsUsed,
            int maxRunoffs = 3, int identicalTieThreshold = 3)
        {
            var sorted = tiedCandidates.OrderBy(id => id).ToArray();
            string key = string.Join(",", sorted);
            if (key == lastTieKey) identicalTieCount++;
            else
            {
                lastTieKey = key;
                identicalTieCount = 1;
            }

            if (identicalTieCount >= identicalTieThreshold) return TieFollowup.RandomEjection;
            return runoffsUsed < maxRunoffs ? TieFollowup.StartRunoff : TieFollowup.NoEjection;
        }

        public void Reset()
        {
            lastTieKey = null;
            identicalTieCount = 0;
        }
    }
}
