using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using DroneSurveillanceSystem.Models;

namespace DroneSurveillanceSystem.Services
{
    public enum DroneAction
    {
        MoveNorth,
        MoveSouth,
        MoveEast,
        MoveWest,
        MoveNorthEast,
        MoveNorthWest,
        MoveSouthEast,
        MoveSouthWest,
        Hover,
        IncreaseAltitude,
        DecreaseAltitude,
        RotateLeft,
        RotateRight,
        ZoomIn,
        ZoomOut
    }

    public class DroneState
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Altitude { get; set; }
        public double Heading { get; set; } // 0-360 degrees
        public double BatteryLevel { get; set; }
        public string CurrentZone { get; set; } = string.Empty;
        public bool IsTargetDetected { get; set; }
        public double DistanceToTarget { get; set; }
        public List<string> DetectedObjects { get; set; } = new();
        public double CameraZoom { get; set; } = 1.0;
        public DateTime StateTime { get; set; } = DateTime.Now;
    }

    public class RLAction
    {
        public DroneAction Action { get; set; }
        public double Confidence { get; set; }
        public double ExpectedReward { get; set; }
        public string Reasoning { get; set; } = string.Empty;
        public DateTime ActionTime { get; set; } = DateTime.Now;
    }

    public class TrainingData
    {
        public DroneState State { get; set; } = new();
        public DroneAction Action { get; set; }
        public double Reward { get; set; }
        public DroneState NextState { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class ReinforcementLearningService
    {
        private readonly Dictionary<string, double> _qTable;
        private readonly List<TrainingData> _trainingHistory;
        private readonly Random _random = new Random();
        
        // RL Parameters
        private const double LearningRate = 0.1;
        private const double DiscountFactor = 0.95;
        private const double ExplorationRate = 0.1;
        
        // Environment bounds
        private const double MinX = -1000;
        private const double MaxX = 1000;
        private const double MinY = -1000;
        private const double MaxY = 1000;
        private const double MinAltitude = 10;
        private const double MaxAltitude = 150;

        public bool IsTrainingMode { get; set; } = true;
        public int TotalActions { get; private set; } = 0;
        public int SuccessfulActions { get; private set; } = 0;
        public double SuccessRate => TotalActions > 0 ? (double)SuccessfulActions / TotalActions : 0.0;
        public int EpisodesCompleted { get; private set; } = 0;

        public ReinforcementLearningService()
        {
            _qTable = new Dictionary<string, double>();
            _trainingHistory = new List<TrainingData>();
            InitializeQTable();
        }

        private void InitializeQTable()
        {
            // Initialize Q-table with small random values
            var actions = Enum.GetValues<DroneAction>();
            var zones = new[] { "Zone-A", "Zone-B", "Zone-C", "Zone-D", "Zone-E" };
            var batteryLevels = new[] { "Low", "Medium", "High" };
            var targetStates = new[] { "Detected", "NotDetected", "Lost" };

            foreach (var zone in zones)
            {
                foreach (var battery in batteryLevels)
                {
                    foreach (var target in targetStates)
                    {
                        foreach (var action in actions)
                        {
                            var stateKey = $"{zone}_{battery}_{target}";
                            var actionKey = $"{stateKey}_{action}";
                            _qTable[actionKey] = _random.NextDouble() * 0.1 - 0.05; // Small random values
                        }
                    }
                }
            }
        }

        public async Task<RLAction> GetOptimalActionAsync(DroneState currentState)
        {
            await Task.Delay(50); // Simulate RL computation time

            var stateKey = EncodeState(currentState);
            var availableActions = GetAvailableActions(currentState);
            
            DroneAction selectedAction;
            double confidence;
            string reasoning;

            if (IsTrainingMode && _random.NextDouble() < ExplorationRate)
            {
                // Exploration: Choose random action
                selectedAction = availableActions[_random.Next(availableActions.Count)];
                confidence = 0.3 + _random.NextDouble() * 0.4; // 30-70% confidence for exploration
                reasoning = "Exploration phase - random action selection";
            }
            else
            {
                // Exploitation: Choose best action from Q-table
                var actionValues = availableActions
                    .Select(action => new { Action = action, Value = GetQValue(stateKey, action) })
                    .OrderByDescending(av => av.Value)
                    .ToList();

                selectedAction = actionValues.First().Action;
                var bestValue = actionValues.First().Value;
                var worstValue = actionValues.Last().Value;
                
                // Calculate confidence based on Q-value differences
                confidence = Math.Max(0.5, Math.Min(0.95, 
                    0.7 + (bestValue - worstValue) * 2));
                
                reasoning = $"Q-learning selection - Q-value: {bestValue:F3}";
            }

            var expectedReward = CalculateExpectedReward(currentState, selectedAction);
            
            TotalActions++;

            return new RLAction
            {
                Action = selectedAction,
                Confidence = confidence,
                ExpectedReward = expectedReward,
                Reasoning = reasoning,
                ActionTime = DateTime.Now
            };
        }

        private List<DroneAction> GetAvailableActions(DroneState state)
        {
            var actions = new List<DroneAction>();

            // Movement actions (always available)
            actions.AddRange(new[] { 
                DroneAction.MoveNorth, DroneAction.MoveSouth, 
                DroneAction.MoveEast, DroneAction.MoveWest,
                DroneAction.MoveNorthEast, DroneAction.MoveNorthWest,
                DroneAction.MoveSouthEast, DroneAction.MoveSouthWest,
                DroneAction.Hover 
            });

            // Altitude actions (with bounds checking)
            if (state.Altitude < MaxAltitude - 10)
                actions.Add(DroneAction.IncreaseAltitude);
            
            if (state.Altitude > MinAltitude + 10)
                actions.Add(DroneAction.DecreaseAltitude);

            // Rotation actions (always available)
            actions.AddRange(new[] { DroneAction.RotateLeft, DroneAction.RotateRight });

            // Camera actions (always available)
            actions.AddRange(new[] { DroneAction.ZoomIn, DroneAction.ZoomOut });

            return actions;
        }

        private string EncodeState(DroneState state)
        {
            var zone = state.CurrentZone;
            var battery = state.BatteryLevel > 70 ? "High" : 
                         state.BatteryLevel > 30 ? "Medium" : "Low";
            var target = state.IsTargetDetected ? "Detected" : 
                        state.DetectedObjects.Any() ? "Lost" : "NotDetected";

            return $"{zone}_{battery}_{target}";
        }

        private double GetQValue(string stateKey, DroneAction action)
        {
            var actionKey = $"{stateKey}_{action}";
            return _qTable.TryGetValue(actionKey, out var value) ? value : 0.0;
        }

        private void UpdateQValue(string stateKey, DroneAction action, double reward, string nextStateKey)
        {
            var actionKey = $"{stateKey}_{action}";
            var currentQ = GetQValue(stateKey, action);
            
            // Find max Q-value for next state
            var nextStateMaxQ = 0.0;
            if (!string.IsNullOrEmpty(nextStateKey))
            {
                var nextActions = Enum.GetValues<DroneAction>();
                nextStateMaxQ = nextActions.Max(a => GetQValue(nextStateKey, a));
            }

            // Q-learning update rule
            var newQ = currentQ + LearningRate * (reward + DiscountFactor * nextStateMaxQ - currentQ);
            _qTable[actionKey] = newQ;
        }

        private double CalculateExpectedReward(DroneState state, DroneAction action)
        {
            double reward = 0.0;

            // Base rewards for different actions
            switch (action)
            {
                case DroneAction.Hover:
                    reward = state.IsTargetDetected ? 0.8 : -0.2; // Good if target detected
                    break;

                case DroneAction.MoveNorth:
                case DroneAction.MoveSouth:
                case DroneAction.MoveEast:
                case DroneAction.MoveWest:
                    reward = 0.1; // Small positive for movement
                    if (state.DistanceToTarget > 0)
                        reward += Math.Max(-0.5, -state.DistanceToTarget / 100); // Reward for moving toward target
                    break;

                case DroneAction.IncreaseAltitude:
                    reward = state.Altitude < 80 ? 0.3 : -0.1; // Good altitude range
                    break;

                case DroneAction.DecreaseAltitude:
                    reward = state.Altitude > 20 ? 0.1 : -0.3; // Avoid too low
                    break;

                case DroneAction.ZoomIn:
                    reward = state.IsTargetDetected ? 0.5 : 0.1;
                    break;

                case DroneAction.ZoomOut:
                    reward = state.IsTargetDetected ? -0.1 : 0.3; // Better for searching
                    break;
            }

            // Battery consideration
            if (state.BatteryLevel < 20)
                reward -= 0.5; // Penalty for low battery

            // Zone coverage bonus
            if (state.CurrentZone != "Zone-A") // Encourage exploration
                reward += 0.1;

            return Math.Max(-1.0, Math.Min(1.0, reward)); // Clamp between -1 and 1
        }

        public async Task<DroneState> SimulateActionAsync(DroneState currentState, DroneAction action)
        {
            await Task.Delay(100); // Simulate action execution time

            var newState = new DroneState
            {
                X = currentState.X,
                Y = currentState.Y,
                Altitude = currentState.Altitude,
                Heading = currentState.Heading,
                BatteryLevel = Math.Max(0, currentState.BatteryLevel - 0.5), // Battery drain
                CurrentZone = currentState.CurrentZone,
                IsTargetDetected = currentState.IsTargetDetected,
                DistanceToTarget = currentState.DistanceToTarget,
                DetectedObjects = new List<string>(currentState.DetectedObjects),
                CameraZoom = currentState.CameraZoom,
                StateTime = DateTime.Now
            };

            // Apply action effects
            const double moveDistance = 20.0;
            const double altitudeChange = 5.0;
            const double rotationAngle = 15.0;

            switch (action)
            {
                case DroneAction.MoveNorth:
                    newState.Y = Math.Min(MaxY, newState.Y + moveDistance);
                    break;
                case DroneAction.MoveSouth:
                    newState.Y = Math.Max(MinY, newState.Y - moveDistance);
                    break;
                case DroneAction.MoveEast:
                    newState.X = Math.Min(MaxX, newState.X + moveDistance);
                    break;
                case DroneAction.MoveWest:
                    newState.X = Math.Max(MinX, newState.X - moveDistance);
                    break;
                case DroneAction.MoveNorthEast:
                    newState.X = Math.Min(MaxX, newState.X + moveDistance * 0.7);
                    newState.Y = Math.Min(MaxY, newState.Y + moveDistance * 0.7);
                    break;
                case DroneAction.MoveNorthWest:
                    newState.X = Math.Max(MinX, newState.X - moveDistance * 0.7);
                    newState.Y = Math.Min(MaxY, newState.Y + moveDistance * 0.7);
                    break;
                case DroneAction.MoveSouthEast:
                    newState.X = Math.Min(MaxX, newState.X + moveDistance * 0.7);
                    newState.Y = Math.Max(MinY, newState.Y - moveDistance * 0.7);
                    break;
                case DroneAction.MoveSouthWest:
                    newState.X = Math.Max(MinX, newState.X - moveDistance * 0.7);
                    newState.Y = Math.Max(MinY, newState.Y - moveDistance * 0.7);
                    break;
                case DroneAction.IncreaseAltitude:
                    newState.Altitude = Math.Min(MaxAltitude, newState.Altitude + altitudeChange);
                    break;
                case DroneAction.DecreaseAltitude:
                    newState.Altitude = Math.Max(MinAltitude, newState.Altitude - altitudeChange);
                    break;
                case DroneAction.RotateLeft:
                    newState.Heading = (newState.Heading - rotationAngle + 360) % 360;
                    break;
                case DroneAction.RotateRight:
                    newState.Heading = (newState.Heading + rotationAngle) % 360;
                    break;
                case DroneAction.ZoomIn:
                    newState.CameraZoom = Math.Min(5.0, newState.CameraZoom * 1.2);
                    break;
                case DroneAction.ZoomOut:
                    newState.CameraZoom = Math.Max(0.5, newState.CameraZoom * 0.8);
                    break;
            }

            // Update zone based on position
            newState.CurrentZone = DetermineZone(newState.X, newState.Y);

            // Simulate detection changes
            if (_random.NextDouble() < 0.1) // 10% chance of detection change
            {
                newState.IsTargetDetected = !newState.IsTargetDetected;
                if (newState.IsTargetDetected)
                {
                    newState.DetectedObjects.Add("target_object");
                    newState.DistanceToTarget = _random.NextDouble() * 100;
                }
                else
                {
                    newState.DetectedObjects.Clear();
                    newState.DistanceToTarget = 0;
                }
            }

            return newState;
        }

        private string DetermineZone(double x, double y)
        {
            // Simple zone mapping based on coordinates
            if (x >= 0 && x < 200 && y >= 0 && y < 200) return "Zone-A";
            if (x >= 200 && x < 400 && y >= 0 && y < 200) return "Zone-B";
            if (x >= 0 && x < 200 && y >= 200 && y < 400) return "Zone-C";
            if (x >= 200 && x < 400 && y >= 200 && y < 400) return "Zone-D";
            return "Zone-E"; // Default zone
        }

        public async Task TrainAsync(DroneState currentState, DroneAction action, double reward, DroneState nextState)
        {
            await Task.Delay(10); // Simulate training computation

            var stateKey = EncodeState(currentState);
            var nextStateKey = EncodeState(nextState);

            // Update Q-table
            UpdateQValue(stateKey, action, reward, nextStateKey);

            // Store training data
            _trainingHistory.Add(new TrainingData
            {
                State = currentState,
                Action = action,
                Reward = reward,
                NextState = nextState,
                Timestamp = DateTime.Now
            });

            // Track success
            if (reward > 0)
                SuccessfulActions++;

            // Limit training history size
            if (_trainingHistory.Count > 10000)
            {
                _trainingHistory.RemoveRange(0, 1000);
            }
        }

        public void CompleteEpisode()
        {
            EpisodesCompleted++;
        }

        public Dictionary<string, object> GetTrainingStatistics()
        {
            return new Dictionary<string, object>
            {
                ["TotalActions"] = TotalActions,
                ["SuccessfulActions"] = SuccessfulActions,
                ["SuccessRate"] = SuccessRate,
                ["EpisodesCompleted"] = EpisodesCompleted,
                ["QTableSize"] = _qTable.Count,
                ["TrainingDataSize"] = _trainingHistory.Count,
                ["ExplorationRate"] = ExplorationRate,
                ["LearningRate"] = LearningRate,
                ["AverageReward"] = _trainingHistory.Any() ? _trainingHistory.Average(t => t.Reward) : 0.0
            };
        }

        public List<TrainingData> GetRecentTrainingData(int count = 100)
        {
            return _trainingHistory.TakeLast(count).ToList();
        }
    }
}
