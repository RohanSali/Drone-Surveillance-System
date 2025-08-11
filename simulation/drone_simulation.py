#!/usr/bin/env python3

"""
DRONE CONTROLLER FOR .ZIP MODEL FILES

This controller can load and use .zip model files from Stable Baselines3 training
to control the drone and navigate to user-specified positions.
"""

import warnings
warnings.filterwarnings("ignore", message=".*Gym version v0.21.0.*")

try:
    import gymnasium as gym
    from gymnasium import Wrapper, spaces
    USING_GYMNASIUM = True
    print("✅ Using Gymnasium")
except ImportError:
    import gym
    from gym import Wrapper, spaces
    USING_GYMNASIUM = False
    print("⚠️ Using legacy Gym")

import numpy as np
import math
import json
import time
import threading
import sys
import os
import zipfile
import ast
os.environ['TF_CPP_MIN_LOG_LEVEL'] = '2'

sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
from gym_pybullet_drones.envs.CtrlAviary import CtrlAviary
from gym_pybullet_drones.utils.enums import DroneModel, Physics
from gym_pybullet_drones.control.DSLPIDControl import DSLPIDControl
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
from inference_engine import runner
from stable_baselines3 import PPO
import pybullet as p
from train_hover_spin3 import make_enhanced_env,make_vec_env

current_dir = os.path.dirname(os.path.abspath(__file__))

models_path = os.path.join(current_dir, "rl_models")
MODEL_FILE_PATH = os.path.join(models_path, "ppo_emergency_takeoff_recovery_working_position_yaw_interrupted.zip")
FOV = 90  # Field of View in degrees
DRONE_CORDS_PATH = os.path.join(current_dir,".." ,"capture_engine","drone_targets.txt")

assets_path = os.path.join(current_dir,"..","environment_assets")

fire_obj_path = os.path.join(assets_path, "city", "city.obj")
building_obj_path = os.path.join(assets_path, "building3", "building.obj")
# Verify files exist
for obj_path in [fire_obj_path, building_obj_path]:
    assert os.path.exists(obj_path), f"OBJ file not found: {obj_path}"

def env_object(object_path,obj_position=[0, 0, 0], obj_orientation=[0, 0, 0],obj_color=[1, 1, 1, 1],obj_base_mass=0,collision_shape=True):
    object = p.createVisualShape(
        shapeType = p.GEOM_MESH,
        fileName = object_path,
        rgbaColor = obj_color,
        specularColor = [0.4, 0.4, 0.4],
        visualFramePosition = [0, 0, 0],
        meshScale = [1, 1, 1]
    )

    if collision_shape:
        object_collisionShapeId = p.createCollisionShape(
            shapeType = p.GEOM_MESH,
            fileName = object_path,
            meshScale = [1, 1, 1]
        )
        p.createMultiBody(
            baseMass = obj_base_mass,
            baseCollisionShapeIndex = object_collisionShapeId,
            baseVisualShapeIndex = object,
            basePosition = obj_position,
            baseOrientation = p.getQuaternionFromEuler(obj_orientation)
        )
    else:
        p.createMultiBody(
            baseMass = obj_base_mass,
            baseVisualShapeIndex = object,
            basePosition = obj_position,
            baseOrientation = p.getQuaternionFromEuler(obj_orientation)
        )

def get_intrinsic_matrix(width, height, fov_deg):
    fov_rad = math.radians(fov_deg)
    fx = width / (2 * math.tan(fov_rad / 2))
    fy = fx  # assuming square pixels
    cx = width / 2
    cy = height / 2
    return np.array([
        [fx, 0, cx],
        [0, fy, cy],
        [0, 0, 1]
    ])

def get_drone_rotation_matrix(drone_ori):
    qw, qx, qy, qz = drone_ori
    return np.array([
        [1 - 2*qy**2 - 2*qz**2, 2*qx*qy - 2*qz*qw, 2*qx*qz + 2*qy*qw],
        [2*qx*qy + 2*qz*qw, 1 - 2*qx**2 - 2*qz**2, 2*qy*qz - 2*qx*qw],
        [2*qx*qz - 2*qy*qw, 2*qy*qz + 2*qx*qw, 1 - 2*qx**2 - 2*qy**2]
    ])

    
class ZipModelDroneWrapper(Wrapper):
    """
    Enhanced wrapper specifically designed to work with .zip model files
    and provide precise position control for user commands.
    """
    
    def __init__(self, env, follow_camera=True, control_mode="position_yaw"):
        super().__init__(env)
        self.ctrl = DSLPIDControl(drone_model=DroneModel.CF2X)
        
        # Control parameters
        self.control_mode = control_mode
        self.follow_camera = follow_camera
        self.CTRL_TIMESTEP = self.env.CTRL_TIMESTEP
        
        # Target management
        self.user_target = None
        self.user_yaw = 0.0
        self.current_target = np.array([0., 0., 3.0], dtype=np.float32)
        self.target_yaw = 0.0
        
        # State tracking
        self.prev_pos = None
        self.step_count = 0
        self.position_history = []
        
        # Flight phases
        self.initialization_phase = True
        self.init_steps = 0
        self.max_init_steps = 200
        self.min_flight_height = 2.0
        
        # Movement parameters
        self.max_speed = 4.0  # Maximum movement speed
        self.position_tolerance = 0.5  # How close to target is "reached"
        self.smoothing_factor = 0.1  # Movement smoothing
        
        # Boundaries
        self.flight_boundaries = {
            'x': [-80.0, 80.0],
            'y': [-80.0, 80.0],
            'z': [1.5, 80.0]
        }
        
        self._setup_action_space()
        print(f"🎯 ZipModelDroneWrapper initialized with {control_mode} control")
    
    def _setup_action_space(self):
        """Setup action space based on control mode"""
        if self.control_mode == "position_only":
            self.action_space = spaces.Box(
                low=np.array([-1.0, -1.0, -1.0], dtype=np.float32),
                high=np.array([1.0, 1.0, 1.0], dtype=np.float32),
                dtype=np.float32
            )
        elif self.control_mode == "position_yaw":
            self.action_space = spaces.Box(
                low=np.array([-1.0, -1.0, -1.0, -1.0], dtype=np.float32),
                high=np.array([1.0, 1.0, 1.0, 1.0], dtype=np.float32),
                dtype=np.float32
            )
        else:  # full_attitude
            self.action_space = spaces.Box(
                low=np.array([-1.0, -1.0, -1.0, -1.0, -2.0, -2.0], dtype=np.float32),
                high=np.array([1.0, 1.0, 1.0, 1.0, 2.0, 2.0], dtype=np.float32),
                dtype=np.float32
            )
    
    def set_user_target(self, x, y, z, yaw_degrees=0):
        """Set target position from user input"""
        # Clamp to boundaries
        x = np.clip(x, self.flight_boundaries['x'][0], self.flight_boundaries['x'][1])
        y = np.clip(y, self.flight_boundaries['y'][0], self.flight_boundaries['y'][1])
        z = np.clip(z, self.flight_boundaries['z'][0], self.flight_boundaries['z'][1])
        
        self.user_target = np.array([x, y, z], dtype=np.float32)
        self.user_yaw = np.radians(yaw_degrees)
        
        print(f"🎯 User target set: [{x:.2f}, {y:.2f}, {z:.2f}] at {yaw_degrees}°")
        print(f"   (Clamped to boundaries if needed)")
    
    def clear_user_target(self):
        """Clear user target - return to hover mode"""
        self.user_target = None
        print("🚁 Returning to hover mode")
    
    def get_current_position(self):
        """Get current drone position"""
        if self.prev_pos is not None:
            return self.prev_pos.copy()
        return np.array([0., 0., 0.], dtype=np.float32)
    
    def _update_target_smoothly(self, current_pos):
        """Update target position with smooth movement"""
        if self.user_target is not None:
            # Move towards user target
            direction = self.user_target - current_pos
            distance = np.linalg.norm(direction)
            
            if distance > self.position_tolerance:
                # Normalize direction and apply max speed
                if distance > 0:
                    direction_normalized = direction / distance
                    max_step = self.max_speed * self.CTRL_TIMESTEP * 50  # Scale for 50Hz
                    step_size = min(max_step, distance)
                    
                    # Smooth movement
                    target_step = current_pos + direction_normalized * step_size
                    self.current_target = (1 - self.smoothing_factor) * self.current_target + \
                                        self.smoothing_factor * target_step
                    self.target_yaw = self.user_yaw
            else:
                # Close enough to user target
                self.current_target = self.user_target.copy()
                self.target_yaw = self.user_yaw
        else:
            # No user target - maintain current position or hover
            if self.prev_pos is not None:
                hover_target = self.prev_pos.copy()
                hover_target[2] = max(hover_target[2], self.min_flight_height)
                self.current_target = hover_target
    
    def reset(self, **kwargs):
        """Reset environment"""
        print("\n🔄 RESET: Initializing new episode for .zip model")
        
        # Reset environment
        result = self.env.reset(**kwargs)
        if USING_GYMNASIUM and isinstance(result, tuple):
            obs, info = result
        else:
            obs = result
            info = {}
        
        env_object(fire_obj_path, obj_position=[0, 0, 0.01], obj_orientation= [np.pi/2, 0, 0], collision_shape=False )  # Fire at origin
        env_object(building_obj_path, obj_position=[7, 0, 0.01], obj_orientation=[np.pi/2, 0, 0], collision_shape=True)  # Building at (7, 0)
        env_object(building_obj_path, obj_position=[8, 10, 0.01], obj_orientation=[np.pi/2, 0, 0], collision_shape=True)  # Building at (8, 10)

        # Reset internal state
        self.initialization_phase = True
        self.init_steps = 0
        self.user_target = None
        self.user_yaw = 0.0
        self.current_target = np.array([0., 0., 3.0], dtype=np.float32)
        self.target_yaw = 0.0
        self.prev_pos = None
        self.step_count = 0
        self.position_history = []
        
        # Extract initial position
        try:
            if hasattr(obs, 'shape') and len(obs.shape) > 1:
                initial_pos = obs[0, 0:3].astype(np.float32).copy()
            else:
                initial_pos = np.array(obs[0:3], dtype=np.float32).copy()
            
            self.prev_pos = initial_pos
            print(f"📍 Initial position: [{initial_pos[0]:.2f}, {initial_pos[1]:.2f}, {initial_pos[2]:.2f}]")
            
        except Exception as e:
            print(f"⚠️ Could not extract initial position: {e}")
            self.prev_pos = np.array([0., 0., 0.5], dtype=np.float32)
        
        print(f"🚁 Ready for .zip model control")
        
        if USING_GYMNASIUM:
            return obs, info
        else:
            return obs
    
    def step(self, action):
        """Execute one step with .zip model integration"""
        self.step_count += 1
        self.init_steps += 1
        
        # Get current observation first
        result = self.env.step(np.zeros((1, 4), dtype=np.float32))
        
        if len(result) == 4:
            observation, _, terminated, info = result
            truncated = False
        else:
            observation, _, terminated, truncated, info = result
        
        # Extract current state
        try:
            if hasattr(observation, 'shape') and len(observation.shape) > 1:
                drone_state = observation[0]
            else:
                drone_state = np.array(observation, dtype=np.float32)
            
            current_pos = drone_state[0:3].astype(np.float32)
            
            # Extract orientation for yaw calculation
            if len(drone_state) > 6:
                quat = drone_state[3:7]
                current_yaw = np.arctan2(2*(quat[3]*quat[2] + quat[0]*quat[1]), 
                                       1 - 2*(quat[1]**2 + quat[2]**2))
            else:
                current_yaw = 0.0
                
        except Exception as e:
            print(f"❌ Error extracting state: {e}")
            current_pos = self.prev_pos if self.prev_pos is not None else np.array([0., 0., 1.], dtype=np.float32)
            current_yaw = 0.0
            drone_state = np.zeros(20, dtype=np.float32)
            drone_state[0:3] = current_pos
        
        # Update position history
        self.position_history.append(current_pos.copy())
        if len(self.position_history) > 100:
            self.position_history.pop(0)
        
        # Update camera
        if self.follow_camera:
            self._update_camera(current_pos)
        
        # Phase management
        if self.initialization_phase:
            if self.init_steps < self.max_init_steps:
                # Initialization phase - get to stable hover
                self.current_target = np.array([0., 0., 3.0], dtype=np.float32)
                self.target_yaw = 0.0
                
                if self.step_count % 50 == 0:
                    print(f"🔄 Initialization: {self.init_steps}/{self.max_init_steps} - "
                          f"Height: {current_pos[2]:.2f}m")
            else:
                self.initialization_phase = False
                print("✅ Initialization complete - Ready for user commands")
        else:
            # Normal operation - follow user targets
            self._update_target_smoothly(current_pos)
        
        # PID Control
        try:
            rpm, _, _ = self.ctrl.computeControlFromState(
                control_timestep=self.CTRL_TIMESTEP,
                state=drone_state,
                target_pos=self.current_target,
                target_rpy=np.array([0, 0, self.target_yaw], dtype=np.float32)
            )
            
            # Ensure minimum RPMs during initialization
            if self.initialization_phase:
                min_rpm = 9000
                rpm = np.maximum(rpm, min_rpm)
                
        except Exception as e:
            print(f"❌ PID control error: {e}")
            rpm = np.array([1000, 1000, 1000, 1000], dtype=np.float32)
        
        # Execute control
        result = self.env.step(np.array([rpm], dtype=np.float32))
        
        if len(result) == 4:
            obs, _, terminated, info = result
            truncated = False
        else:
            obs, _, terminated, truncated, info = result
        
        # Calculate reward (simple distance-based)
        reward = self._calculate_reward(current_pos, terminated, truncated)
        
        # Progress logging
        if self.step_count % 100 == 0 and not self.initialization_phase:
            distance_to_target = np.linalg.norm(current_pos - self.current_target) if self.user_target is not None else 0
            user_distance = np.linalg.norm(current_pos - self.user_target) if self.user_target is not None else 0
            
            print(f"📊 Step {self.step_count}: Pos [{current_pos[0]:.2f}, {current_pos[1]:.2f}, {current_pos[2]:.2f}]")
            if self.user_target is not None:
                print(f"   🎯 User target distance: {user_distance:.2f}m, Current target distance: {distance_to_target:.2f}m")
        
        self.prev_pos = current_pos
        
        if USING_GYMNASIUM:
            return obs, reward, terminated, truncated, info
        else:
            return obs, reward, terminated, info
    
    def _update_camera(self, drone_pos):
        """Update camera to follow drone"""
        #setting camera parameters
        CAMERA_OFFSET_POS = [0.15, 0, 0.05] # 15cm forward, 0cm sideways, 5cm up (relative to drone base)
        CAMERA_OFFSET_TARGET = [0.3, 0, 0.05] # Pointing 30cm forward, 5cm up (relative to drone base)
        CAMERA_UP_VECTOR = [0, 0, 1] # Generally Z-up relative to the world for a stable image
        try:
            p.resetDebugVisualizerCamera(
                cameraDistance=6.0,
                cameraYaw=45,
                cameraPitch=-30,
                cameraTargetPosition=drone_pos
            )

            drone_pos, drone_ori = p.getBasePositionAndOrientation(1)

            camera_world_pos, _ = p.multiplyTransforms(drone_pos, drone_ori, CAMERA_OFFSET_POS, [0, 0, 0, 1])
            camera_world_target, _ = p.multiplyTransforms(drone_pos, drone_ori, CAMERA_OFFSET_TARGET, [0, 0, 0, 1])
            viewMatrix = p.computeViewMatrix(
                cameraEyePosition=camera_world_pos,
                cameraTargetPosition=camera_world_target,
                cameraUpVector=CAMERA_UP_VECTOR # Using a world-aligned up vector
            )

            projectionMatrix = p.computeProjectionMatrixFOV(
            fov=FOV, # Field of View (adjust as needed)
            aspect=float(640)/480, # Aspect Ratio (Width/Height of the image)
            nearVal=0.01, # Near clipping plane
            farVal=100 # Far clipping plane
            )

            # Get camera image
            width, height, rgb_img, depth_img, seg_img = p.getCameraImage( width=224, height=224, viewMatrix=viewMatrix,projectionMatrix=projectionMatrix, renderer=p.ER_BULLET_HARDWARE_OPENGL)

            p.configureDebugVisualizer(p.COV_ENABLE_SEGMENTATION_MARK_PREVIEW, 1)
            p.configureDebugVisualizer(p.COV_ENABLE_DEPTH_BUFFER_PREVIEW, 1)
            p.configureDebugVisualizer(p.COV_ENABLE_RGB_BUFFER_PREVIEW, 1)

            rgb_a = np.reshape(rgb_img, (height, width, 4))[:, :, :3]
            rgb_array = rgb_a.astype(np.uint8)
            intrinsic_matrix = get_intrinsic_matrix(width, height, fov_deg=FOV)
            rotation_matrix = get_drone_rotation_matrix(drone_ori)

            runner.runner_sim(rgb_array, intrinsic_matrix, rotation_matrix,drone_pos)
        except:
            pass

    def is_target_reached(self):
        """Check if drone has reached the user target"""
        if self.user_target is None:
            return True  # No target set
        
        current_pos = self.get_current_position()
        distance = np.linalg.norm(current_pos - self.user_target)
        return distance < self.position_tolerance
    
    def get_target_distance(self):
        """Get distance to current target"""
        if self.user_target is None:
            return 0.0
        
        current_pos = self.get_current_position()
        return np.linalg.norm(current_pos - self.user_target)

    def target_reached(self):
        """Property to check if target is reached (alternative name)"""
        return self.is_target_reached()
    
    def _calculate_reward(self, current_pos, terminated, truncated):
        """Simple reward calculation"""
        reward = 0.0
        
        # Height reward
        height = current_pos[2]
        if height < 1.0:
            reward -= 100  # Penalty for being too low
        elif 1.5 <= height <= 6.0:
            reward += 10   # Reward for good height
        
        # Target following reward
        if self.user_target is not None:
            distance = np.linalg.norm(current_pos - self.user_target)
            reward += max(0, 20 - distance * 5)  # Closer = better
            
            if distance < self.position_tolerance:
                reward += 50  # Bonus for reaching target
        
        # Penalty for termination
        if terminated or truncated:
            reward -= 200
        
        # Small time penalty
        reward -= 0.1
        
        return reward

class ZipModelDroneController:
    """
    Main controller class for using .zip model files to control the drone
    """
    
    def __init__(self, zip_model_path, hover_height=3.0):
        """
        Initialize drone controller with .zip model
        
        Args:
            zip_model_path: Path to the .zip model file
            hover_height: Default hover height
        """
        self.zip_model_path = zip_model_path
        self.hover_height = hover_height
        self.running = False
        self.step_count = 0
        self.position_history = []
        
        # Verify model file
        if not os.path.exists(zip_model_path):
            raise FileNotFoundError(f"Model file not found: {zip_model_path}")
        
        if not zip_model_path.endswith('.zip'):
            raise ValueError("Model file must be a .zip file")
        
        print(f"🔍 Loading model from: {zip_model_path}")
        self._inspect_model_file()
        
        # Create environment
        print("🔧 Creating environment for .zip model...")
        base_env = CtrlAviary(
            drone_model=DroneModel.CF2X,
            num_drones=1,
            physics=Physics.PYB,
            gui=True,
            record=False
        )
        
        # Use specialized wrapper
        self.env = ZipModelDroneWrapper(
            base_env,
            follow_camera=True,
            control_mode="position_yaw"
        )
        
        # Load the model
        try:
            self.model = PPO.load(zip_model_path)
            print(f"✅ Successfully loaded .zip model!")
        except Exception as e:
            print(f"❌ Failed to load .zip model: {e}")
            raise
        
        # Initialize environment
        reset_result = self.env.reset()
        if isinstance(reset_result, tuple):
            self.obs, info = reset_result
        else:
            self.obs = reset_result
        
        print(f"🚁 Drone controller ready with .zip model")
        print(f"   Model: {os.path.basename(zip_model_path)}")
        print(f"   Hover height: {hover_height}m")
    
    def _inspect_model_file(self):
        """Inspect the contents of the .zip model file"""
        try:
            with zipfile.ZipFile(self.zip_model_path, 'r') as zip_file:
                files = zip_file.namelist()
                print(f"📦 Model file contents: {files}")
                
                # Check for expected files
                expected_files = ['data', 'parameters', 'pytorch_variables.pth']
                for expected in expected_files:
                    if expected in files:
                        print(f"   ✅ Found: {expected}")
                    else:
                        print(f"   ⚠️ Missing: {expected}")
                        
        except Exception as e:
            print(f"⚠️ Could not inspect model file: {e}")
    
    def get_position(self):
        """Get current drone position"""
        return self.env.get_current_position()
    
    def get_velocity(self):
        """Calculate current velocity"""
        if len(self.position_history) < 2:
            return np.array([0, 0, 0], dtype=np.float32)
        
        dt = 0.02  # 50Hz
        current_pos = self.position_history[-1]
        prev_pos = self.position_history[-2]
        velocity = (current_pos - prev_pos) / dt
        return velocity
    
    def go_to_position(self, x, y, z, yaw_degrees=0):
        """Command drone to go to specific position"""
        print(f"🎯 Setting target: [{x:.2f}, {y:.2f}, {z:.2f}] at {yaw_degrees}°")
        self.env.set_user_target(x, y, z, yaw_degrees)
        return [x, y, z]
    
    def hover_here(self):
        """Command drone to hover at current position"""
        current_pos = self.get_position()
        hover_pos = [current_pos[0], current_pos[1], max(current_pos[2], self.hover_height)]
        self.env.set_user_target(hover_pos[0], hover_pos[1], hover_pos[2], 0)
        print(f"🚁 Hovering at: [{hover_pos[0]:.2f}, {hover_pos[1]:.2f}, {hover_pos[2]:.2f}]")
        return hover_pos
    
    def stop_movement(self):
        """Stop current movement and hover"""
        self.env.clear_user_target()
        print("🛑 Movement stopped - entering hover mode")
    
    def run_step(self):
        """Execute one simulation step"""
        try:
            # Get action from the .zip model
            action, _ = self.model.predict(self.obs, deterministic=True)
            
            # Execute step
            step_result = self.env.step(action)
            
            if len(step_result) == 5:
                self.obs, reward, terminated, truncated, info = step_result
            else:
                self.obs, reward, terminated, info = step_result
                truncated = False
            
            self.step_count += 1
            
            # Update position history
            current_pos = self.get_position()
            self.position_history.append(current_pos.copy())
            if len(self.position_history) > 50:
                self.position_history.pop(0)
            
            return not (terminated or truncated)
            
        except Exception as e:
            print(f"❌ Error in simulation step: {e}")
            return False
    
    def start_simulation(self):
        """Start the simulation loop"""
        self.running = True
        
        def simulation_loop():
            print("🎮 Starting simulation with .zip model...")
            
            while self.running:
                if not self.run_step():
                    print("⚠️ Simulation ended")
                    break
                time.sleep(0.02)  # 50Hz
            
            print("🛑 Simulation stopped")
        
        self.sim_thread = threading.Thread(target=simulation_loop, daemon=True)
        self.sim_thread.start()
        print("✅ Background simulation started")
    
    def wait_for_initialization(self, timeout=30.0):
        """Wait for drone to complete initialization"""
        print("⏳ Waiting for drone initialization...")
        start_time = time.time()
        
        while time.time() - start_time < timeout:
            if hasattr(self.env, 'initialization_phase') and not self.env.initialization_phase:
                print("✅ Drone initialization complete!")
                return True
            time.sleep(0.5)
        
        print(f"⚠️ Initialization timeout after {timeout}s")
        return False
    
    def wait_for_target(self, target_pos, tolerance=1.0, timeout=20.0):
        """Wait for drone to reach target position"""
        target = np.array(target_pos[:3], dtype=np.float32)
        start_time = time.time()
        
        print(f"⏳ Waiting to reach [{target[0]:.2f}, {target[1]:.2f}, {target[2]:.2f}] within {tolerance}m...")
        
        min_distance = float('inf')
        last_update = start_time
        
        while time.time() - start_time < timeout:
            current_pos = self.get_position()
            distance = np.linalg.norm(current_pos - target)
            
            if distance < min_distance:
                min_distance = distance
            
            if distance < tolerance:
                elapsed = time.time() - start_time
                print(f"✅ Target reached in {elapsed:.1f}s! Final distance: {distance:.3f}m")
                return True
            
            # Progress updates
            if time.time() - last_update >= 3.0:
                elapsed = time.time() - start_time
                vel = self.get_velocity()
                vel_mag = np.linalg.norm(vel)
                print(f"   📍 {elapsed:.1f}s: Distance {distance:.2f}m (best: {min_distance:.2f}m), Vel: {vel_mag:.1f}m/s")
                last_update = time.time()
            
            time.sleep(0.1)
        
        final_distance = np.linalg.norm(self.get_position() - target)
        print(f"⚠️ Timeout: Final distance {final_distance:.3f}m (best: {min_distance:.3f}m)")
        return final_distance < tolerance * 1.5
    
    def stop(self):
        """Stop the controller"""
        print("🛑 Stopping drone controller...")
        self.running = False
        
        if hasattr(self, 'sim_thread'):
            self.sim_thread.join(timeout=3.0)
        
        self.env.close()
        print("✅ Controller stopped")

def interactive_demo_zip():
    """Interactive demo for .zip model files"""
    print("🚁 DRONE CONTROLLER FOR .ZIP MODEL FILES")
    print("="*60)
    
    # # Get model file from user
    # model_file = input("📁 Enter path to your .zip model file: ").strip()
    model_file = MODEL_FILE_PATH

    if not model_file:
        print("❌ No model file specified")
        return
    
    try:
        # Initialize controller
        controller = ZipModelDroneController(model_file, hover_height=3.0)
        
        # Start simulation
        controller.start_simulation()
        
        # Wait for initialization
        if not controller.wait_for_initialization(timeout=30.0):
            print("⚠️ Proceeding without full initialization...")
        
         # Process initial coordinates
        position_queue = load_coordinates_from_file(DRONE_CORDS_PATH)
        print(f"📋 Loaded {len(position_queue)} coordinates")
        
        def execute_coordinate_queue(coords):
            """Execute a list of coordinates"""
            for i, coord in enumerate(coords):
                name, x, y, z, yaw = coord
                print(f"🎯 Going to position {i+1}/{len(coords)}: {name}")
                controller.go_to_position(x, y, z, yaw)
                
                # Wait for drone to reach target
                while not controller.env.is_target_reached():
                    time.sleep(0.1)
                
                print(f"✅ Reached {name}")
                time.sleep(1)  # Brief pause between waypoints
        
        # Execute initial coordinates
        if position_queue:
            execute_coordinate_queue(position_queue)
            print("🏁 Initial waypoints completed!")
        
        # Start continuous monitoring for new coordinates
        print("🔄 Starting continuous monitoring for new coordinates...")
        print("💡 Add new coordinates to drone_targets.txt and they'll be processed automatically")
        print("💡 Press 'q' and Enter to quit")
        
        import threading
        import queue
        
        # Create a queue for communication between threads
        command_queue = queue.Queue()
        running = True
        
        def monitor_coordinates():
            """Monitor file for new coordinates in a separate thread"""
            nonlocal running
            last_check = time.time()
            
            while running:
                try:
                    # Check for new coordinates every 2 seconds
                    if time.time() - last_check >= 2.0:
                        new_coords = load_coordinates_from_file(DRONE_CORDS_PATH)
                        
                        if new_coords:
                            print(f"🆕 Found {len(new_coords)} new coordinates!")
                            execute_coordinate_queue(new_coords)
                            print("✅ New coordinates completed!")
                        
                        last_check = time.time()
                    
                    time.sleep(0.5)  # Small sleep to prevent excessive CPU usage
                    
                except Exception as e:
                    print(f"⚠️ Error monitoring coordinates: {e}")
        
        # Start coordinate monitoring in a separate thread
        monitor_thread = threading.Thread(target=monitor_coordinates, daemon=True)
        monitor_thread.start()
        
        # Main thread handles user input
        while running:
            try:
                command = input().strip().lower()
                if command == 'q':
                    running = False
                    break
            except KeyboardInterrupt:
                running = False
                break
        
        print("🛑 Stopping coordinate monitoring...")

    except Exception as e:
        print(f"❌ Error: {e}")
    
    finally:
        if 'controller' in locals():
            controller.stop()
    # Add these methods to your EnhancedPPOPIDHybridWrapper class

def load_coordinates_from_file(file_path=DRONE_CORDS_PATH):
    position = []
    lines_to_keep = []
    
    try:
        with open(file_path, 'r') as file:
            lines = file.readlines()
        
        for line_num, line in enumerate(lines, 1):
            # line = line.strip()
            line = ast.literal_eval(line.strip())
            if not line:
                lines_to_keep.append(line + '\n')
                continue
            
            try:
                # Parse your format: {"targetId":1;"location":[0, 0, 8, 0]}
                # location = json.loads(line)
                
                if isinstance(line, dict) and 'targetId' in line and 'location' in line:
                    target_id = line.get('targetId')
                    coords = line.get('location')
                    
                    if len(coords) >= 4:
                        x, y, z, yaw = coords[0], coords[1], coords[2], coords[3]
                        position.append([f"target_{target_id}", x, y, z, yaw])
                        print(f"Loaded: target_{target_id} -> [{x}, {y}, {z}, {yaw}°]")
                        # Line gets deleted by NOT adding to lines_to_keep
                    else:
                        lines_to_keep.append(line + '\n')
                else:
                    lines_to_keep.append(line + '\n')
                    
            except Exception as e:
                print(f"Invalid JSON on line {line_num}: {line} , error: {e}")
                lines_to_keep.append(line + '\n')
        
        # Rewrite file with only unprocessed lines
        with open(file_path, 'w') as file:
            file.writelines(lines_to_keep)
            
    except Exception as e:
        print(f"Error reading file: {e}")
    
    return position

def set_continuous_spin(self, angular_velocity=0.5, direction="clockwise"):
    """
    Make the drone spin continuously
    angular_velocity: radians per second (0.5 = ~28.6 degrees/sec)
    direction: "clockwise" or "counterclockwise"
    """
    self.continuous_spin = True
    self.spin_velocity = angular_velocity if direction == "counterclockwise" else -angular_velocity
    self.spin_enabled = True
    print(f"🌀 Continuous spin enabled: {direction} at {np.degrees(abs(angular_velocity)):.1f}°/sec")

def stop_spin(self):
    """Stop continuous spinning"""
    self.continuous_spin = False
    self.spin_enabled = False
    print("🛑 Spin stopped")

def rotate_to_angle(self, target_angle_degrees, rotation_speed=1.0):
    """
    Rotate drone to a specific angle
    target_angle_degrees: target yaw angle in degrees (0-360)
    rotation_speed: how fast to rotate (radians per second)
    """
    target_yaw_rad = np.radians(target_angle_degrees)
    self.target_yaw = target_yaw_rad
    self.rotation_speed = rotation_speed
    self.rotating_to_target = True
    print(f"🎯 Rotating to {target_angle_degrees}° at speed {np.degrees(rotation_speed):.1f}°/sec")

def set_spin_pattern(self, pattern="figure8", speed=0.3):
    """
    Set predefined spinning patterns
    pattern: "figure8", "circle", "square", "random"
    """
    self.spin_pattern = pattern
    self.pattern_speed = speed
    self.pattern_enabled = True
    self.pattern_time = 0
    print(f"🎨 Spin pattern enabled: {pattern} at speed {speed}")

# Add these variables to your __init__ method:
def __init__(self, env, follow_camera=False, control_mode="position_yaw"):
    # ... existing init code ...
    
    # Add rotation control variables
    self.continuous_spin = False
    self.spin_velocity = 0.0
    self.spin_enabled = False
    self.rotating_to_target = False
    self.rotation_speed = 1.0
    self.spin_pattern = None
    self.pattern_enabled = False
    self.pattern_speed = 0.3
    self.pattern_time = 0
    
    # ... rest of existing init code ...

# Modify your step method to include rotation logic:
def step(self, action):
    self.step_count += 1
    self.force_takeoff_steps += 1
    
    # ... existing step code until PID control section ...
    
    # ROTATION CONTROL LOGIC (add this before PID control)
    if self.spin_enabled or self.pattern_enabled:
        self._update_rotation_target()
    
    # ... continue with existing PID control code ...

def _update_rotation_target(self):
    """Update target yaw based on rotation settings"""
    dt = self.CTRL_TIMESTEP
    
    if self.continuous_spin:
        # Continuous spinning
        self.target_yaw += self.spin_velocity * dt
        # Keep yaw in [-π, π] range
        self.target_yaw = (self.target_yaw + np.pi) % (2 * np.pi) - np.pi
        
    elif self.pattern_enabled:
        # Pattern-based rotation
        self.pattern_time += dt
        
        if self.spin_pattern == "figure8":
            # Figure-8 pattern
            self.target_yaw = np.sin(self.pattern_time * self.pattern_speed) * np.pi/2
            
        elif self.spin_pattern == "circle":
            # Circular motion
            self.target_yaw = self.pattern_time * self.pattern_speed
            
        elif self.spin_pattern == "square":
            # Square pattern (90-degree turns)
            cycle_time = 4.0 / self.pattern_speed  # 4 sides
            side = int((self.pattern_time % cycle_time) / (cycle_time / 4))
            self.target_yaw = side * np.pi/2
            
        elif self.spin_pattern == "random":
            # Random rotation every few seconds
            if self.step_count % int(3.0 / dt) == 0:  # Every 3 seconds
                self.target_yaw = np.random.uniform(-np.pi, np.pi)

# Example usage functions to add to your main code:
def demonstrate_rotations():
    """Demonstrate different rotation capabilities"""
    
    # Create environment
    vec_env = make_vec_env(lambda: make_enhanced_env(follow_camera=True, control_mode="position_yaw"), n_envs=1)
    
    # Get the wrapper (to access rotation methods)
    wrapper = vec_env.envs[0].env
    
    print("🚁 Starting rotation demonstrations...")
    
    # Reset environment
    obs = vec_env.reset()
    
    # Wait for takeoff
    for i in range(200):
        action = vec_env.action_space.sample() * 0  # No movement
        obs, reward, done, info = vec_env.step([action])
        if done:
            obs = vec_env.reset()
    
    print("✅ Takeoff complete, starting rotations...")
    
    # Demo 1: Spin clockwise for 5 seconds
    wrapper.set_continuous_spin(angular_velocity=1.0, direction="clockwise")
    for i in range(250):  # ~5 seconds at 50fps
        action = vec_env.action_space.sample() * 0
        obs, reward, done, info = vec_env.step([action])
        if done:
            obs = vec_env.reset()
    
    # Demo 2: Stop and rotate to 90 degrees
    wrapper.stop_spin()
    wrapper.rotate_to_angle(90)  # Face east
    for i in range(150):
        action = vec_env.action_space.sample() * 0
        obs, reward, done, info = vec_env.step([action])
        if done:
            obs = vec_env.reset()
    
    # Demo 3: Figure-8 pattern
    wrapper.set_spin_pattern("figure8", speed=0.5)
    for i in range(500):  # ~10 seconds
        action = vec_env.action_space.sample() * 0
        obs, reward, done, info = vec_env.step([action])
        if done:
            obs = vec_env.reset()
    
    # Demo 4: Random rotations
    wrapper.set_spin_pattern("random", speed=1.0)
    for i in range(600):  # ~12 seconds
        action = vec_env.action_space.sample() * 0
        obs, reward, done, info = vec_env.step([action])
        if done:
            obs = vec_env.reset()
    
    vec_env.close()
    print("🏁 Rotation demonstration complete!")

if __name__ == "__main__":
    print("🎯 DRONE CONTROLLER FOR .ZIP MODEL FILES")
    print("This controller can load and use .zip model files from Stable Baselines3 training")
    print("Features:")
    print("- ✅ Loads .zip model files directly")
    print("- ✅ Smooth position control to user-specified locations")
    print("- ✅ Real-time command input without blocking")
    print("- ✅ Safety boundaries and height limits")
    print("- ✅ Interactive demo modes")
    print("- ✅ Progress tracking and status updates")
    print("\n🚀 Starting interactive demo...")
    
    interactive_demo_zip()