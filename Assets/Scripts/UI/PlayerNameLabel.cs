using System.Globalization;

namespace LuckyNumber.UI
{
    public static class PlayerNameLabel
    {
        public static string Compact(string name, int limit = 20)
        {
            var elements = StringInfo.ParseCombiningCharacters(name);
            if (elements.Length <= limit) return name;
            int tail = limit / 3;
            int head = limit - tail - 1;
            return name[..elements[head]] + "…" + name[elements[elements.Length - tail]..];
        }
    }
}
