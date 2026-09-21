namespace Aurora.Contracts;

public sealed record RoadPointDto(double Latitude, double Longitude);
public sealed record RoadRouteRequestDto(List<RoadPointDto> Points, string Profile);
public sealed record RoadDirectionDto(string Description, double Latitude, double Longitude);
public sealed record RoadRouteDto(List<RoadPointDto> Points, List<RoadDirectionDto> Directions);
