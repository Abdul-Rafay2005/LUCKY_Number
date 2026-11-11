namespace LuckyNumber.Core
{
    public static class WinRules
    {
        public static Team GetRoleCountWinner(int livingImposters, int livingNormals)
        {
            if (livingImposters <= 0) return Team.NormalTeam;
            if (livingImposters >= livingNormals) return Team.ImposterTeam;
            return Team.None;
        }
    }
}
