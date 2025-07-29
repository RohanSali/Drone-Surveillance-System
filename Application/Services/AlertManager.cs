using System.Collections.ObjectModel;
using DroneSurveillanceSystem.Views;

namespace DroneSurveillanceSystem.Services
{
    public class AlertManager
    {
        private static AlertManager? _instance;
        public static AlertManager Instance => _instance ??= new AlertManager();

        public ObservableCollection<AlertData> ActiveAlerts { get; } = new ObservableCollection<AlertData>();

        private AlertManager() { }

        /// <summary>
        /// Clears all active alerts from the collection
        /// </summary>
        public void ClearAllAlerts()
        {
            ActiveAlerts.Clear();
        }

        /// <summary>
        /// Gets the current count of active alerts
        /// </summary>
        public int AlertCount => ActiveAlerts.Count;

        /// <summary>
        /// Checks if there are any active alerts
        /// </summary>
        public bool HasAlerts => ActiveAlerts.Count > 0;

        /// <summary>
        /// Manually clears all alerts (can be called from UI or other services)
        /// </summary>
        public void ManualClearAlerts()
        {
            ClearAllAlerts();
        }

        /// <summary>
        /// Resets the singleton instance to ensure a completely fresh start
        /// This should be called on application startup to clear any persistent data
        /// </summary>
        public static void ResetInstance()
        {
            _instance = null;
        }
    }
} 