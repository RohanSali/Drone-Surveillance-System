namespace DroneSurveillanceSystem.Services
{
    /// <summary>
    /// In-memory persistence for Alert Validation target fields so values survive closing/reopening the control panel.
    /// </summary>
    public sealed class ControlPanelValidationState
    {
        private static readonly ControlPanelValidationState _instance = new();
        public static ControlPanelValidationState Instance => _instance;

        private ControlPanelValidationState() { }

        public string Latitude { get; set; } = "0.0";
        public string Longitude { get; set; } = "0.0";
        public string Altitude { get; set; } = "0";
        public string Yaw { get; set; } = "0";
    }
}
