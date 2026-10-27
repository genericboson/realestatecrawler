using System.Text;

namespace RealEstateCrawler.Geo;

internal static class Geohash
{
    const string Base32 = "0123456789bcdefghjkmnpqrstuvwxyz";

    public static string Encode(double lat, double lng, int precision)
    {
        double latMin = -90, latMax = 90, lngMin = -180, lngMax = 180;
        var hash = new StringBuilder(precision);
        var evenBit = true;
        int bit = 0, index = 0;

        while (hash.Length < precision)
        {
            if (evenBit)
            {
                var mid = (lngMin + lngMax) / 2;
                if (lng >= mid) { index = index * 2 + 1; lngMin = mid; }
                else { index *= 2; lngMax = mid; }
            }
            else
            {
                var mid = (latMin + latMax) / 2;
                if (lat >= mid) { index = index * 2 + 1; latMin = mid; }
                else { index *= 2; latMax = mid; }
            }
            evenBit = !evenBit;

            if (++bit == 5)
            {
                hash.Append(Base32[index]);
                bit = 0;
                index = 0;
            }
        }
        return hash.ToString();
    }

    /// <summary>사각형 영역을 빈틈없이 덮는 geohash 셀 목록.</summary>
    public static IReadOnlyList<string> Covering(GeoBounds bounds, int precision)
    {
        var bits = precision * 5;
        var latStep = 180 / Math.Pow(2, bits / 2) / 2;
        var lngStep = 360 / Math.Pow(2, (bits + 1) / 2) / 2;

        var cells = new SortedSet<string>(StringComparer.Ordinal);
        for (var lat = bounds.South; ; lat += latStep)
        {
            var clampedLat = Math.Min(lat, bounds.North);
            for (var lng = bounds.West; ; lng += lngStep)
            {
                var clampedLng = Math.Min(lng, bounds.East);
                cells.Add(Encode(clampedLat, clampedLng, precision));
                if (clampedLng >= bounds.East) break;
            }
            if (clampedLat >= bounds.North) break;
        }
        return [.. cells];
    }
}
