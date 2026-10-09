using System.Collections;
using System.Text.RegularExpressions;

namespace SOGMAPanelManager;

public sealed class NaturalStringComparer : IComparer<string>, IComparer
{
    private static readonly Regex Parts = new(@"\d+|\D+", RegexOptions.Compiled);

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var xParts = Parts.Matches(x);
        var yParts = Parts.Matches(y);
        int count = Math.Min(xParts.Count, yParts.Count);

        for (int i = 0; i < count; i++)
        {
            string a = xParts[i].Value;
            string b = yParts[i].Value;

            bool aNum = long.TryParse(a, out long aValue);
            bool bNum = long.TryParse(b, out long bValue);

            int result;
            if (aNum && bNum)
            {
                result = aValue.CompareTo(bValue);
                if (result == 0)
                    result = a.Length.CompareTo(b.Length);
            }
            else
            {
                result = string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
            }

            if (result != 0)
                return result;
        }

        return xParts.Count.CompareTo(yParts.Count);
    }

    int IComparer.Compare(object? x, object? y) => Compare(x?.ToString(), y?.ToString());
}
