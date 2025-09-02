using Firebase.Database;
using Firebase.Database.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DroneSurveillanceSystem.Models;
using DroneSurveillanceSystem.Services;
using Newtonsoft.Json;

namespace DroneSurveillanceSystem.Services
{
    public class FirebaseService
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly string _databaseUrl = "YOUR_FIREBASE_DATABASE_URL"; // Replace with your Firebase database URL
        private readonly string _authSecret = "YOUR_FIREBASE_AUTH_SECRET"; // Replace with your Firebase auth secret

        public FirebaseService()
        {
            _firebaseClient = new FirebaseClient(
                _databaseUrl,
                new FirebaseOptions
                {
                    AuthTokenAsyncFactory = () => Task.FromResult(_authSecret)
                });
        }

        // Network Operations
        public async Task<string> SaveNetworkAsync(Network network)
        {
            try
            {
                var result = await _firebaseClient
                    .Child("networks")
                    .PostAsync(JsonConvert.SerializeObject(network));

                return result.Key;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save network: {ex.Message}");
            }
        }

        public async Task<List<Network>> GetNetworksAsync()
        {
            try
            {
                var networks = await _firebaseClient
                    .Child("networks")
                    .OnceAsync<Network>();

                return networks.Select(x => x.Object).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get networks: {ex.Message}");
            }
        }

        public async Task<Network> GetNetworkAsync(string networkId)
        {
            try
            {
                var network = await _firebaseClient
                    .Child("networks")
                    .Child(networkId)
                    .OnceSingleAsync<Network>();

                return network;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get network: {ex.Message}");
            }
        }

        public async Task UpdateNetworkAsync(string networkId, Network network)
        {
            try
            {
                await _firebaseClient
                    .Child("networks")
                    .Child(networkId)
                    .PutAsync(JsonConvert.SerializeObject(network));
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update network: {ex.Message}");
            }
        }

        public async Task DeleteNetworkAsync(string networkId)
        {
            try
            {
                await _firebaseClient
                    .Child("networks")
                    .Child(networkId)
                    .DeleteAsync();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to delete network: {ex.Message}");
            }
        }

        // Device Operations
        public async Task<string> SaveDeviceAsync(SurveillanceDevice device)
        {
            try
            {
                var result = await _firebaseClient
                    .Child("devices")
                    .PostAsync(JsonConvert.SerializeObject(device));

                return result.Key;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save device: {ex.Message}");
            }
        }

        public async Task<List<SurveillanceDevice>> GetDevicesAsync()
        {
            try
            {
                var devices = await _firebaseClient
                    .Child("devices")
                    .OnceAsync<SurveillanceDevice>();

                return devices.Select(x => x.Object).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get devices: {ex.Message}");
            }
        }

        public async Task<List<SurveillanceDevice>> GetDevicesByTypeAsync(string deviceType)
        {
            try
            {
                var devices = await _firebaseClient
                    .Child("devices")
                    .OrderBy("DeviceType")
                    .EqualTo(deviceType)
                    .OnceAsync<SurveillanceDevice>();

                return devices.Select(x => x.Object).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get devices by type: {ex.Message}");
            }
        }

        // Alert Operations
        public async Task<string> SaveAlertAsync(SurveillanceAlert alert)
        {
            try
            {
                var result = await _firebaseClient
                    .Child("alerts")
                    .PostAsync(JsonConvert.SerializeObject(alert));

                return result.Key;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save alert: {ex.Message}");
            }
        }

        public async Task<List<SurveillanceAlert>> GetAlertsAsync()
        {
            try
            {
                var alerts = await _firebaseClient
                    .Child("alerts")
                    .OrderBy("Timestamp")
                    .OnceAsync<SurveillanceAlert>();

                return alerts.Select(x => x.Object).OrderByDescending(x => x.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get alerts: {ex.Message}");
            }
        }

        public async Task<List<SurveillanceAlert>> GetAlertsByStatusAsync(string status)
        {
            try
            {
                var alerts = await _firebaseClient
                    .Child("alerts")
                    .OrderBy("Status")
                    .EqualTo(status)
                    .OnceAsync<SurveillanceAlert>();

                return alerts.Select(x => x.Object).OrderByDescending(x => x.Timestamp).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get alerts by status: {ex.Message}");
            }
        }

        public async Task UpdateAlertStatusAsync(string alertId, string status)
        {
            try
            {
                await _firebaseClient
                    .Child("alerts")
                    .Child(alertId)
                    .Child("Status")
                    .PutAsync(status);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to update alert status: {ex.Message}");
            }
        }

        // User Operations
        public async Task<string> SaveUserAsync(UserProfile user)
        {
            try
            {
                var result = await _firebaseClient
                    .Child("users")
                    .PostAsync(JsonConvert.SerializeObject(user));

                return result.Key;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to save user: {ex.Message}");
            }
        }

        public async Task<UserProfile> GetUserByEmailAsync(string email)
        {
            try
            {
                var users = await _firebaseClient
                    .Child("users")
                    .OrderBy("Email")
                    .EqualTo(email)
                    .OnceAsync<UserProfile>();

                return users.FirstOrDefault()?.Object;
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to get user by email: {ex.Message}");
            }
        }

        // Real-time listeners
        public IObservable<FirebaseObject<Network>> GetNetworksRealtime()
        {
            return _firebaseClient
                .Child("networks")
                .AsObservable<Network>();
        }

        public IObservable<FirebaseObject<SurveillanceAlert>> GetAlertsRealtime()
        {
            return _firebaseClient
                .Child("alerts")
                .AsObservable<SurveillanceAlert>();
        }

        public void Dispose()
        {
            _firebaseClient?.Dispose();
        }
    }
}
