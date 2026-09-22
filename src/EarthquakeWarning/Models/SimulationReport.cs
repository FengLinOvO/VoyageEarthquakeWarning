namespace Voyage.EarthquakeWarning.Models;

public sealed class SimulationReport : ObservableObject
{
    private string _placeName = "甘肃临夏州积石山县";
    private double _longitude = 102.80;
    private double _latitude = 35.75;
    private double _magnitude = 5.9;
    private double _epiIntensity = 7.9;
    private double? _depth = 10;
    private int _updates = 1;
    private double _alertDelaySeconds = 4.9;

    public string PlaceName { get => _placeName; set => SetProperty(ref _placeName, value); }
    public double Longitude { get => _longitude; set => SetProperty(ref _longitude, value); }
    public double Latitude { get => _latitude; set => SetProperty(ref _latitude, value); }
    public double Magnitude { get => _magnitude; set => SetProperty(ref _magnitude, value); }
    public double EpiIntensity { get => _epiIntensity; set => SetProperty(ref _epiIntensity, value); }
    public double? Depth { get => _depth; set => SetProperty(ref _depth, value); }
    public int Updates { get => _updates; set => SetProperty(ref _updates, value); }
    public double AlertDelaySeconds { get => _alertDelaySeconds; set => SetProperty(ref _alertDelaySeconds, value); }
}
