using System.Collections.Generic;
using NUnit.Framework;
using LuckyNumber.Core;

namespace LuckyNumber.Tests
{
    public class VoteRulesTests
    {
        [Test]
        public void TwoSkipBallotsBeatOneSuspectBallot()
        {
            var votes = new Dictionary<ulong, ulong> { [10] = 100 };
            var skips = new HashSet<ulong> { 11, 12 };

            var result = VoteRules.Tally(votes, skips, new ulong[] { 100, 200 }, new ulong[] { 10, 11, 12 });

            Assert.AreEqual(VoteResolutionKind.SkipEjection, result.Kind);
            Assert.AreEqual(2, result.VotesAtLead);
            Assert.IsEmpty(result.LeadingCandidates);
        }

        [Test]
        public void SkipTiedForTheLeadPreventsAnElimination()
        {
            var votes = new Dictionary<ulong, ulong> { [10] = 100 };
            var skips = new HashSet<ulong> { 11 };

            var result = VoteRules.Tally(votes, skips, new ulong[] { 100, 200 }, new ulong[] { 10, 11 });

            Assert.AreEqual(VoteResolutionKind.SkipEjection, result.Kind);
        }

        [Test]
        public void UniqueSuspectLeaderIsReturnedForElimination()
        {
            var votes = new Dictionary<ulong, ulong> { [10] = 100, [11] = 100, [12] = 200 };

            var result = VoteRules.Tally(votes, new HashSet<ulong>(), new ulong[] { 100, 200 }, new ulong[] { 10, 11, 12 });

            Assert.AreEqual(VoteResolutionKind.Eliminate, result.Kind);
            CollectionAssert.AreEqual(new ulong[] { 100 }, result.LeadingCandidates);
        }

        [Test]
        public void NoBallotsAndCandidateTiesRemainDistinctOutcomes()
        {
            var noVotes = VoteRules.Tally(new Dictionary<ulong, ulong>(), new HashSet<ulong>(),
                new ulong[] { 100, 200 }, new ulong[] { 10, 11 });
            var tied = VoteRules.Tally(new Dictionary<ulong, ulong> { [10] = 200, [11] = 100 },
                new HashSet<ulong>(), new ulong[] { 100, 200 }, new ulong[] { 10, 11 });

            Assert.AreEqual(VoteResolutionKind.NoVotes, noVotes.Kind);
            Assert.AreEqual(VoteResolutionKind.TiedCandidates, tied.Kind);
            CollectionAssert.AreEqual(new ulong[] { 100, 200 }, tied.LeadingCandidates);
        }

        [Test]
        public void VotesForDepartedCandidatesAreIgnored()
        {
            var votes = new Dictionary<ulong, ulong> { [10] = 999, [11] = 100 };

            var result = VoteRules.Tally(votes, new HashSet<ulong>(), new ulong[] { 100, 200 }, new ulong[] { 10, 11 });

            Assert.AreEqual(VoteResolutionKind.Eliminate, result.Kind);
            CollectionAssert.AreEqual(new ulong[] { 100 }, result.LeadingCandidates);
        }

        [Test]
        public void AllSkipBallotsChooseSkipEjection()
        {
            var skips = new HashSet<ulong> { 10, 11, 12 };
            var result = VoteRules.Tally(new Dictionary<ulong, ulong>(), skips,
                new ulong[] { 100, 200 }, new ulong[] { 10, 11, 12 });

            Assert.AreEqual(VoteResolutionKind.SkipEjection, result.Kind);
            Assert.AreEqual(3, result.VotesAtLead);
        }

        [Test]
        public void SuspectWinsWhenTheirBallotsBeatSkipEjection()
        {
            var votes = new Dictionary<ulong, ulong> { [10] = 100, [11] = 100 };
            var result = VoteRules.Tally(votes, new HashSet<ulong> { 12 },
                new ulong[] { 100, 200 }, new ulong[] { 10, 11, 12 });

            Assert.AreEqual(VoteResolutionKind.Eliminate, result.Kind);
            CollectionAssert.AreEqual(new ulong[] { 100 }, result.LeadingCandidates);
        }

        [Test]
        public void DepartedVoterAndDepartedCandidateBallotsAreIgnored()
        {
            var votes = new Dictionary<ulong, ulong> { [10] = 999, [11] = 100, [99] = 200 };
            var skips = new HashSet<ulong> { 98 };
            var result = VoteRules.Tally(votes, skips, new ulong[] { 100, 200 }, new ulong[] { 10, 11 });

            Assert.AreEqual(VoteResolutionKind.Eliminate, result.Kind);
            CollectionAssert.AreEqual(new ulong[] { 100 }, result.LeadingCandidates);
        }

        [Test]
        public void ThreeIdenticalTiesCountInitialResultAndChooseRandomEjection()
        {
            var tracker = new VoteRunoffTracker();
            ulong[] firstOrder = { 200, 100 };

            Assert.AreEqual(TieFollowup.StartRunoff, tracker.RegisterTie(firstOrder, 0));
            Assert.AreEqual(TieFollowup.StartRunoff, tracker.RegisterTie(new ulong[] { 100, 200 }, 1));
            Assert.AreEqual(TieFollowup.RandomEjection, tracker.RegisterTie(firstOrder, 2));
        }

        [Test]
        public void ChangedTieSetResetsCounterAndFinalRunoffEndsWithoutEjection()
        {
            var tracker = new VoteRunoffTracker();
            tracker.RegisterTie(new ulong[] { 100, 200 }, 0);
            Assert.AreEqual(TieFollowup.StartRunoff,
                tracker.RegisterTie(new ulong[] { 100, 300 }, 1));
            Assert.AreEqual(TieFollowup.StartRunoff,
                tracker.RegisterTie(new ulong[] { 100, 400 }, 2));
            Assert.AreEqual(TieFollowup.NoEjection,
                tracker.RegisterTie(new ulong[] { 100, 500 }, 3));
        }

        [Test]
        public void RoleCountRulesCoverLastImposterAndParityWins()
        {
            Assert.AreEqual(Team.NormalTeam, WinRules.GetRoleCountWinner(0, 3));
            Assert.AreEqual(Team.None, WinRules.GetRoleCountWinner(1, 3));
            Assert.AreEqual(Team.ImposterTeam, WinRules.GetRoleCountWinner(2, 2));
            Assert.AreEqual(Team.ImposterTeam, WinRules.GetRoleCountWinner(3, 2));
        }
    }
}
