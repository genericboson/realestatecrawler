namespace RealEstateCrawler.Geo;

/// <summary>중심점과 반경으로 정한 검색 영역.</summary>
public sealed record SearchArea(double CenterLat, double CenterLng, double RadiusKm)
{
    const double EarthRadiusKm = 6371.0088;
    const double KmPerLatDegree = 110.574;
    const double KmPerLngDegreeAtEquator = 111.320;

    // 사이트가 돌려주는 좌표가 살짝 흔들리므로 사각형 조회는 원보다 10% 넓게 잡는다
    const double BoundsMargin = 1.1;

    public double DistanceKm(double lat, double lng)
    {
        var dLat = ToRadians(lat - CenterLat);
        var dLng = ToRadians(lng - CenterLng);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(CenterLat)) * Math.Cos(ToRadians(lat)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    public bool Contains(double lat, double lng) => DistanceKm(lat, lng) <= RadiusKm;

    public GeoBounds Bounds
    {
        get
        {
            var dLat = RadiusKm * BoundsMargin / KmPerLatDegree;
            var dLng = RadiusKm * BoundsMargin / (KmPerLngDegreeAtEquator * Math.Cos(ToRadians(CenterLat)));
            return new GeoBounds(CenterLat - dLat, CenterLng - dLng, CenterLat + dLat, CenterLng + dLng);
        }
    }

    static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

public readonly record struct GeoBounds(double South, double West, double North, double East);
