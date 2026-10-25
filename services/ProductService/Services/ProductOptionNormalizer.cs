namespace ProductService.Services;

public static class ProductOptionNormalizer
{
    public static bool TryNormalize(IEnumerable<string>? values, int maxCount, int maxLength, out List<string> normalized)
    {
        normalized = [];

        foreach (var raw in values ?? [])
        {
            if (raw is null)
                return false;

            var value = raw.Trim();
            if (value.Length == 0)
                continue;
            if (value.Length > maxLength)
                return false;

            if (!normalized.Contains(value, StringComparer.OrdinalIgnoreCase))
                normalized.Add(value);
            if (normalized.Count > maxCount)
                return false;
        }

        return true;
    }
}
