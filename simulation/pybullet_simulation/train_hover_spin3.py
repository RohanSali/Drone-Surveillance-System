#!/usr/bin/env python3

# EMERGENCY FIX FOR DRONE NOT TAKING OFF + WORKING CRASH RECOVERY

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
import time
import sys
import os
os.environ['TF_CPP_MIN_LOG_LEVEL'] = '2'

from gym_pybullet_drones.envs.CtrlAviary import CtrlAviary
from gym_pybullet_drones.utils.enums import DroneModel, Physics
from gym_pybullet_drones.control.DSLPIDControl import DSLPIDControl
from stable_baselines3 import PPO
from stable_baselines3.common.env_util import make_vec_env
import pybullet as p

#setting camera parameters


current_dir = os.path.dirname(os.path.abspath(__file__))

# fire_obj_path = os.path.join(current_dir, "Environment Assets", "fire.obj")
# building_obj_path = os.path.join(current_dir, "Environment Assets", "building.obj")
# print("Loading .obj from:", fire_obj_path)
# assert os.path.exists(fire_obj_path), "OBJ file not found!"


CAMERA_OFFSET_POS = [0.15, 0, 0.05] # 15cm forward, 0cm sideways, 5cm up (relative to drone base)
CAMERA_OFFSET_TARGET = [0.3, 0, 0.05] # Pointing 30cm forward, 5cm up (relative to drone base)
CAMERA_UP_VECTOR = [0, 0, 1] # Generally Z-up relative to the world for a stable image

class EnhancedPPOPIDHybridWrapper(Wrapper):
    def __init__(self, env, follow_camera=False, control_mode="position_yaw"):
        super().__init__(env)
        self.ctrl = DSLPIDControl(drone_model=DroneModel.CF2X)
        
        # CRITICAL FIX: Always start with high target
        self.last_target = np.array([0., 0., 3.0], dtype=np.float32)  # Start at 3m!
        self.initial_pos = np.array([0., 0., 1.], dtype=np.float32)
        self.CTRL_TIMESTEP = self.env.CTRL_TIMESTEP
        
        self.control_mode = control_mode
        self.force_target = None
        self.force_yaw = None
        self.follow_camera = follow_camera
        self.target_yaw = 0.0
        
        # Tracking variables
        self.prev_pos = None
        self.prev_distance_to_target = None
        self.steps_at_target = 0
        self.step_count = 0
        
        # CRITICAL: Always force takeoff
        self.takeoff_phase = True
        self.takeoff_steps = 0
        self.min_flight_height = 2.0  # RAISED from 1.0 to 2.0
        self.takeoff_target_height = 3.0  # RAISED from 2.0 to 3.0
        self.force_takeoff_steps = 0
        self.max_force_takeoff_steps = 500  # Force takeoff for 500 steps minimum
        
        # CRASH DETECTION AND RECOVERY
        self.crash_detected = False
        self.crash_recovery_active = False
        self.recovery_steps = 0
        self.max_recovery_steps = 100
        self.last_stable_orientation = None
        self.ground_contact_threshold = 0.3  # Height threshold for ground contact
        self.flip_threshold = 0.5  # Orientation threshold for flip detection
        self.crash_consecutive_steps = 0
        self.crash_detection_threshold = 5  # Need 5 consecutive bad steps to declare crash
        
        # Store original spawn position and orientation
        self.original_spawn_pos = np.array([0., 0., 1.0], dtype=np.float32)
        self.original_spawn_orientation = np.array([0., 0., 0., 1.], dtype=np.float32)  # Identity quaternion
        
        # Boundaries - CRITICAL: Raised minimum z
        self.boundary_limits = {
            'x': [-5.0, 5.0],   
            'y': [-5.0, 5.0],   
            'z': [2.0, 6.0]     # MINIMUM z is now 2.0!
        }
        
        self._setup_action_space()
        
        print(f"🚁 INITIALIZED: Takeoff phase ON, target height {self.takeoff_target_height}m")
        print(f"🛡️ CRASH RECOVERY: Ground threshold {self.ground_contact_threshold}m, flip threshold {self.flip_threshold}")
    
    def _setup_action_space(self):
        """Setup action space - PREVENT downward movement during training"""
        if self.control_mode == "position_only":
            self.action_space = spaces.Box(
                low=np.array([-1.0, -1.0, 0.0], dtype=np.float32),  # NO negative z!
                high=np.array([1.0, 1.0, 2.0], dtype=np.float32),   # Strong positive z
                dtype=np.float32
            )
        elif self.control_mode == "position_yaw":
            self.action_space = spaces.Box(
                low=np.array([-1.0, -1.0, 0.0, -1.0], dtype=np.float32),  # NO negative z!
                high=np.array([1.0, 1.0, 2.0, 1.0], dtype=np.float32),     # Strong positive z
                dtype=np.float32
            )
        elif self.control_mode == "full_attitude":
            self.action_space = spaces.Box(
                low=np.array([-1.0, -1.0, 0.0, -1.0, -2.0, -2.0], dtype=np.float32),
                high=np.array([1.0, 1.0, 2.0, 1.0, 2.0, 2.0], dtype=np.float32),
                dtype=np.float32
            )
    
    def _detect_crash(self, pos, orientation):
        """Detect if drone has crashed or flipped"""
        height = pos[2]
        
        # Check for ground contact
        ground_contact = height < self.ground_contact_threshold
        
        # Check for flip (upside down) - using quaternion to check if drone is upside down
        # Extract roll and pitch from quaternion
        try:
            quat = orientation
            # Convert quaternion to roll, pitch, yaw
            roll = np.arctan2(2*(quat[3]*quat[0] + quat[1]*quat[2]), 1 - 2*(quat[0]**2 + quat[1]**2))
            pitch = np.arcsin(np.clip(2*(quat[3]*quat[1] - quat[2]*quat[0]), -1.0, 1.0))
            
            # Check if drone is significantly tilted (flipped)
            flip_detected = abs(roll) > self.flip_threshold or abs(pitch) > self.flip_threshold
            
            if self.step_count % 50 == 0:  # Debug info every 50 steps
                print(f"🔍 CRASH CHECK: Height={height:.3f}, Roll={np.degrees(roll):.1f}°, Pitch={np.degrees(pitch):.1f}°, Ground={ground_contact}, Flip={flip_detected}")
                
        except Exception as e:
            print(f"⚠️ Orientation check error: {e}")
            flip_detected = False
        
        # Crash if ground contact OR flipped
        crash_condition = ground_contact or flip_detected
        
        if crash_condition:
            self.crash_consecutive_steps += 1
            if self.crash_consecutive_steps >= self.crash_detection_threshold:
                if not self.crash_detected:
                    print(f"💥 CRASH DETECTED! Height: {height:.3f}m, Ground: {ground_contact}, Flip: {flip_detected}, Roll: {np.degrees(roll):.1f}°, Pitch: {np.degrees(pitch):.1f}°")
                return True
        else:
            self.crash_consecutive_steps = 0
            
        return False
    
    def _reset_drone_to_spawn(self):
        """Reset drone to original spawn position and orientation"""
        try:
            drone_id = self.env.DRONE_IDS[0]  # Get the first (and only) drone ID
            
            # Reset position
            p.resetBasePositionAndOrientation(
                drone_id,
                self.original_spawn_pos.tolist(),
                self.original_spawn_orientation.tolist()
            )
            
            # Reset velocity (both linear and angular)
            p.resetBaseVelocity(drone_id, [0, 0, 0], [0, 0, 0])
            
            print(f"🔄 DRONE RESET: Position {self.original_spawn_pos}, Orientation restored")
            return True
            
        except Exception as e:
            print(f"❌ Failed to reset drone: {e}")
            return False
    
    def _handle_crash_recovery(self, pos, orientation):
        """Handle crash recovery process"""
        if not self.crash_recovery_active:
            # Start recovery process
            self.crash_recovery_active = True
            self.recovery_steps = 0
            print(f"🛠️ STARTING CRASH RECOVERY")
            
            # Reset drone to spawn position
            if self._reset_drone_to_spawn():
                # Reset internal state
                self.takeoff_phase = True
                self.force_takeoff_steps = 0
                self.last_target = np.array([0., 0., 3.0], dtype=np.float32)
                self.target_yaw = 0.0
                self.prev_pos = self.original_spawn_pos.copy()
                print(f"✅ RECOVERY: Drone reset successful, restarting takeoff sequence")
            else:
                print(f"❌ RECOVERY: Failed to reset drone physically")
        
        self.recovery_steps += 1
        
        # During recovery, force upward movement with high RPMs
        recovery_target = np.array([0., 0., 3.0], dtype=np.float32)
        self.last_target = recovery_target
        
        # Check if recovery is complete
        current_height = pos[2]
        if current_height > 1.5 and self.recovery_steps > 20:  # Give some time and height check
            self.crash_recovery_active = False
            self.crash_detected = False
            self.recovery_steps = 0
            self.crash_consecutive_steps = 0
            print(f"🎉 RECOVERY COMPLETE: Drone back to stable flight at {current_height:.3f}m")
            return False  # Recovery complete
        
        if self.recovery_steps > self.max_recovery_steps:
            print(f"⚠️ RECOVERY TIMEOUT: Forcing completion after {self.max_recovery_steps} steps")
            self.crash_recovery_active = False
            self.crash_detected = False
            self.recovery_steps = 0
            self.crash_consecutive_steps = 0
            return False
            
        return True  # Recovery still active
    
    def set_external_target(self, target_pos, target_yaw=None):
        """External target with minimum height enforcement"""
        self.force_target = np.array(target_pos, dtype=np.float32).copy()
        # CRITICAL: Force minimum height
        self.force_target[2] = max(self.force_target[2], self.min_flight_height)
        self.last_target = self.force_target.copy()
        
        if target_yaw is not None:
            self.force_yaw = float(target_yaw)
            self.target_yaw = float(target_yaw)
        
        print(f"✅ External target set to: {self.force_target} (min height enforced)")
    
    def reset(self, **kwargs):
        print("\n🔄 RESET: Starting new episode")

                     # Load your custom OBJ model after PyBullet is connected

        visualShapeId = p.createVisualShape(
            shapeType=p.GEOM_MESH,
            fileName=fire_obj_path,
            rgbaColor=[1,1,1, 1],
            specularColor=[0.4, 0.4, 0.4],
            visualFramePosition=[0, 0, 0],
            meshScale=[1, 1, 1]
        )

        # collisionShapeId = p.createCollisionShape(
        #     shapeType=p.GEOM_MESH,
        #     fileName=fire_obj_path,
        #     meshScale=[1, 1, 1]
        # )

        p.createMultiBody(
            baseMass=0,
            # baseCollisionShapeIndex=collisionShapeId,
            baseVisualShapeIndex=visualShapeId,
            basePosition=[0, 0, 0.01],
            baseOrientation=p.getQuaternionFromEuler([np.pi/2, 0, 0])
        )

        visualShapeId = p.createVisualShape(
            shapeType=p.GEOM_MESH,
            fileName=building_obj_path,
            rgbaColor=[1,1,1, 1],
            specularColor=[0.4, 0.4, 0.4],
            visualFramePosition=[0, 0, 0],
            meshScale=[1, 1, 1]
        )

        # collisionShapeId = p.createCollisionShape(
        #     shapeType=p.GEOM_MESH,
        #     fileName=fire_obj_path,
        #     meshScale=[1, 1, 1]
        # )

        p.createMultiBody(
            baseMass=0,
            # baseCollisionShapeIndex=collisionShapeId,
            baseVisualShapeIndex=visualShapeId,
            basePosition=[7, 00, 0.01],
            baseOrientation=p.getQuaternionFromEuler([np.pi/2, 0, 0])
        )
        p.createMultiBody(
            baseMass=0,
            # baseCollisionShapeIndex=collisionShapeId,
            baseVisualShapeIndex=visualShapeId,
            basePosition=[8, 10, 0.01],
            baseOrientation=p.getQuaternionFromEuler([np.pi/2, 0, 0])
        )
        
        # Handle both gym formats
        result = self.env.reset(**kwargs)
        if USING_GYMNASIUM and isinstance(result, tuple):
            obs, info = result
        else:
            obs = result
            info = {}
        
        # CRITICAL: Always reset to takeoff mode
        self.takeoff_phase = True
        self.takeoff_steps = 0
        self.force_takeoff_steps = 0
        
        # CRITICAL: Reset crash detection variables
        self.crash_detected = False
        self.crash_recovery_active = False
        self.recovery_steps = 0
        self.crash_consecutive_steps = 0
        print(f"🛡️ CRASH DETECTION RESET: All variables cleared")
        
        # CRITICAL: Always start with high target
        if self.force_target is None:
            self.last_target = np.array([0., 0., 3.0], dtype=np.float32)  # 3m target!
            self.target_yaw = 0.0
            print(f"🎯 Reset target to: {self.last_target}")
        else:
            self.last_target = np.array(self.force_target, dtype=np.float32)
            self.last_target[2] = max(self.last_target[2], self.min_flight_height)
            if self.force_yaw is not None:
                self.target_yaw = self.force_yaw
            print(f"🎯 Using external target: {self.last_target}")
        
        # Reset tracking
        self.prev_pos = None
        self.prev_distance_to_target = None
        self.steps_at_target = 0
        self.step_count = 0
        
        # Get initial position
        try:
            if hasattr(obs, 'shape') and len(obs.shape) > 1:
                self.initial_pos = obs[0, 0:3].astype(np.float32).copy()
            elif isinstance(obs, (list, tuple)) and len(obs) > 0:
                self.initial_pos = np.array(obs[0][0:3], dtype=np.float32).copy()
            else:
                self.initial_pos = np.array(obs[0:3], dtype=np.float32).copy()
                
            print(f"📍 Initial position: {self.initial_pos}")
            self.prev_pos = self.initial_pos.copy()
            self.prev_distance_to_target = np.linalg.norm(self.initial_pos - self.last_target)
        except Exception as e:
            print(f"⚠️ Could not extract initial position: {e}")
            self.initial_pos = np.array([0., 0., 0.5], dtype=np.float32)  # Start low
            self.prev_pos = self.initial_pos.copy()
            self.prev_distance_to_target = np.linalg.norm(self.initial_pos - self.last_target)
        
        print(f"🚁 TAKEOFF PHASE ACTIVE: Target height {self.takeoff_target_height}m")
        
        if USING_GYMNASIUM:
            return obs, info
        else:
            return obs
    
    def _update_camera_to_follow_drone(self, drone_pos):
        """Update camera"""
        try:
            p.resetDebugVisualizerCamera(
                cameraDistance=4.0,
                cameraYaw=45,
                cameraPitch=-25,
                cameraTargetPosition=drone_pos
            )
        except:
            pass

    def _calculate_enhanced_reward(self, current_pos, current_yaw, action, terminated, truncated):
        """EMERGENCY REWARD: Massive height focus - KEPT EXACTLY AS REQUESTED"""
        reward = 0.0
        height = float(current_pos[2])
        
        print(f"💰 Reward calc: height={height:.3f}, takeoff_phase={self.takeoff_phase}")
        
        # 1. EMERGENCY HEIGHT REWARDS - Make this dominate everything
        if height < 0.5:
            reward += -500  # MASSIVE penalty for ground contact
            print(f"🚨 GROUND CONTACT PENALTY: -500")
        elif height < 2.0:
            reward += height * 100  # Linear reward up to 200
            print(f"📈 CLIMBING REWARD: {height * 100:.1f}")
        elif 2.0 <= height <= 4.0:
            reward += 300 + (height - 2.0) * 50  # 300-400 range
            print(f"✅ GOOD HEIGHT REWARD: {300 + (height - 2.0) * 50:.1f}")
        else:
            reward += 200    # Too high but still better than ground
        
        # 2. TAKEOFF PHASE: Override everything else
        if self.takeoff_phase or self.force_takeoff_steps < self.max_force_takeoff_steps:
            print(f"🚁 TAKEOFF MODE: step {self.force_takeoff_steps}/{self.max_force_takeoff_steps}")
            
            # Force upward movement during takeoff
            if self.prev_pos is not None:
                vertical_movement = height - float(self.prev_pos[2])
                if vertical_movement > 0.001:  # Any upward movement
                    vertical_reward = vertical_movement * 2000  # HUGE reward
                    reward += vertical_reward
                    print(f"🆙 UPWARD MOVEMENT REWARD: {vertical_reward:.1f}")
                elif vertical_movement <= 0:
                    penalty = -200  # Penalty for not climbing
                    reward += penalty
                    print(f"🔻 NO CLIMB PENALTY: {penalty}")
            
            # Bonus just for being in takeoff mode and getting higher
            altitude_bonus = height * 100  # 100 per meter
            reward += altitude_bonus
            print(f"🎈 ALTITUDE BONUS: {altitude_bonus:.1f}")
            
            # Check if takeoff complete
            if height >= self.takeoff_target_height:
                self.takeoff_phase = False
                completion_bonus = 1000
                reward += completion_bonus
                print(f"🎉 TAKEOFF COMPLETE BONUS: {completion_bonus}")
        
        # 3. POSITION REWARDS (only after takeoff)
        if not self.takeoff_phase and self.force_takeoff_steps >= self.max_force_takeoff_steps:
            distance_to_target = float(np.linalg.norm(current_pos - self.last_target))
            
            if self.prev_distance_to_target is not None:
                progress = self.prev_distance_to_target - distance_to_target
                progress_reward = progress * 50
                reward += progress_reward
                print(f"🎯 POSITION PROGRESS: {progress_reward:.1f}")
        
        # 4. ANTI-CRASH PENALTIES
        if terminated or truncated:
            if height < 1.0:
                crash_penalty = -5000  # MASSIVE crash penalty
                reward += crash_penalty
                print(f"💥 CRASH PENALTY: {crash_penalty}")
                # NOTE: Removed problematic vec_env.reset() call
            else:
                reward += 500  # Bonus for ending at good height
        
        # 5. Minimal time penalty
        reward -= 0.1
        
        # Update tracking
        if self.prev_pos is not None:
            self.prev_distance_to_target = float(np.linalg.norm(current_pos - self.last_target))
        self.prev_pos = current_pos.astype(np.float32).copy()
        
        print(f"💰 TOTAL REWARD: {reward:.2f}\n")
        return float(reward)

    def step(self, action):

        self.step_count += 1
        self.force_takeoff_steps += 1
        
        # Ensure action is numpy array
        action = np.array(action, dtype=np.float32)
        
        # Get current observation
        result = self.env.step(np.zeros((1, 4), dtype=np.float32))
        
        if len(result) == 4:
            observation, _, terminated, info = result
            truncated = False
        else:
            observation, _, terminated, truncated, info = result
        
        # Extract drone state
        try:
            if hasattr(observation, 'shape') and len(observation.shape) > 1:
                drone_state = observation[0]
            elif isinstance(observation, (list, tuple)) and len(observation) > 0:
                drone_state = np.array(observation[0], dtype=np.float32)
            else:
                drone_state = np.array(observation, dtype=np.float32)
                
            pos = drone_state[0:3].astype(np.float32)
            
            if len(drone_state) > 6:
                quat = drone_state[3:7]
                current_yaw = float(np.arctan2(2*(quat[3]*quat[2] + quat[0]*quat[1]), 
                                       1 - 2*(quat[1]**2 + quat[2]**2)))
            else:
                current_yaw = 0.0
                quat = np.array([0., 0., 0., 1.], dtype=np.float32)  # Default quaternion
                
        except Exception as e:
            print(f"❌ Error extracting drone state: {e}")
            pos = self.prev_pos if self.prev_pos is not None else np.array([0., 0., 0.5], dtype=np.float32)
            current_yaw = 0.0
            quat = np.array([0., 0., 0., 1.], dtype=np.float32)
            drone_state = np.zeros(20, dtype=np.float32)
            drone_state[0:3] = pos
            drone_state[3:7] = quat

        # *** CRITICAL FIX: ACTUALLY CALL CRASH DETECTION ***
        crash_detected = self._detect_crash(pos, quat)
        if crash_detected:
            self.crash_detected = True

        # *** CRITICAL FIX: ACTUALLY HANDLE CRASH RECOVERY ***
        if self.crash_detected or self.crash_recovery_active:
            recovery_active = self._handle_crash_recovery(pos, quat)
            if recovery_active:
                # During recovery, force specific behavior
                print(f"🛠️ RECOVERY ACTIVE: Step {self.recovery_steps}/{self.max_recovery_steps}")
        
        # Update camera
        if self.follow_camera:
            self._update_camera_to_follow_drone(pos)
        
        # CRITICAL: FORCED TAKEOFF LOGIC OR RECOVERY
        if ((self.takeoff_phase or self.force_takeoff_steps < self.max_force_takeoff_steps) and self.force_target is None and not self.crash_recovery_active):
            # IGNORE PPO ACTIONS - Force upward movement
            current_height = pos[2]
            
            if self.crash_recovery_active:
                target_height = 3.0  # Recovery target
                print(f"🛠️ RECOVERY CONTROL: Forcing to {target_height}m")
            else:
                target_height = max(current_height + 0.2, self.takeoff_target_height)  # Always climb
                print(f"🚁 FORCED TAKEOFF: Current {current_height:.3f}m -> Target {target_height:.3f}m")
            
            # Force target upward
            forced_target = np.array([0.0, 0.0, target_height], dtype=np.float32)
            self.last_target = forced_target
            
        else:
            # Normal PPO control (only after forced takeoff period and no crash)
            print(f"🎮 PPO CONTROL ACTIVE")
            
            if self.force_target is None:
                if self.control_mode == "position_yaw":
                    relative_move = action[:3].copy()
                    # Still prevent downward movement
                    relative_move = np.clip(relative_move, [-0.8, -0.8, 0.0], [0.8, 0.8, 1.0])
                    new_target = pos + relative_move
                    
                    yaw_change = np.clip(action[3], -0.5, 0.5)
                    self.target_yaw += yaw_change
                    self.target_yaw = (self.target_yaw + np.pi) % (2 * np.pi) - np.pi
                    
                # Enforce boundaries
                for i, axis in enumerate(['x', 'y', 'z']):
                    min_val, max_val = self.boundary_limits[axis]
                    new_target[i] = np.clip(new_target[i], min_val, max_val)
                
                self.last_target = new_target
            else:
                # External target
                self.last_target[2] = max(self.last_target[2], self.min_flight_height)
        
        # PID Control with emergency RPMs
        try:
            rpm, _, _ = self.ctrl.computeControlFromState(
                control_timestep=self.CTRL_TIMESTEP,
                state=drone_state,
                target_pos=self.last_target,
                target_rpy=np.array([0, 0, self.target_yaw], dtype=np.float32)
            )
            
            # CRITICAL: Ensure minimum RPMs for takeoff or recovery
            if self.takeoff_phase or self.force_takeoff_steps < self.max_force_takeoff_steps or self.crash_recovery_active:
                min_rpm = 4200  # High RPMs for takeoff/recovery
                rpm = np.maximum(rpm, min_rpm)
                if self.crash_recovery_active:
                    print(f"🔧 RPM boosted for recovery: {rpm}")
                else:
                    print(f"🔧 RPM boosted for takeoff: {rpm}")
                
        except Exception as e:
            print(f"❌ PID error: {e}")
            # Emergency high RPMs
            rpm = np.array([3500, 3500, 3500, 3500], dtype=np.float32)
            print(f"🚨 EMERGENCY RPMs: {rpm}")
        
        # Execute control
        result = self.env.step(np.array([rpm], dtype=np.float32))
        
        if len(result) == 4:
            obs, _, terminated, info = result
            truncated = False
        else:
            obs, _, terminated, truncated, info = result
        
        # Get new position
        try:
            if hasattr(obs, 'shape') and len(obs.shape) > 1:
                new_pos = obs[0, 0:3].astype(np.float32)
                if obs.shape[1] > 6:
                    new_quat = obs[0, 3:7]
                    new_yaw = float(np.arctan2(2*(new_quat[3]*new_quat[2] + new_quat[0]*new_quat[1]), 
                                       1 - 2*(new_quat[1]**2 + new_quat[2]**2)))
                else:
                    new_yaw = current_yaw
            else:
                new_pos = pos
                new_yaw = current_yaw
        except Exception as e:
            print(f"❌ Error extracting new position: {e}")
            new_pos = pos
            new_yaw = current_yaw
        
        # Calculate reward (kept exactly as requested)
        reward = self._calculate_enhanced_reward(new_pos, new_yaw, action, terminated, truncated)
        
        # Enhanced logging
        if self.step_count % 25 == 0:  # More frequent logging
            distance = float(np.linalg.norm(new_pos - self.last_target))
            yaw_deg = np.degrees(new_yaw)
            target_yaw_deg = np.degrees(self.target_yaw)
            
            if self.crash_recovery_active:
                phase_status = f"RECOVERY ({self.recovery_steps}/{self.max_recovery_steps})"
            elif self.takeoff_phase or self.force_takeoff_steps < self.max_force_takeoff_steps:
                phase_status = f"TAKEOFF ({self.force_takeoff_steps}/{self.max_force_takeoff_steps})"
            else:
                phase_status = "FLIGHT"
            
            target_status = "EXTERNAL" if self.force_target is not None else "PPO"
            
            print(f"📊 Step {self.step_count}: Pos: [{new_pos[0]:.2f}, {new_pos[1]:.2f}, {new_pos[2]:.2f}], "
                  f"Target: [{self.last_target[0]:.2f}, {self.last_target[1]:.2f}, {self.last_target[2]:.2f}], "
                  f"Distance: {distance:.3f}, Yaw: {yaw_deg:.1f}°/{target_yaw_deg:.1f}°, "
                  f"Reward: {reward:.2f}, Mode: {target_status}, Phase: {phase_status}")
        
        drone_pos, drone_ori = p.getBasePositionAndOrientation(1)
        projectionMatrix = p.computeProjectionMatrixFOV(
            fov=90, # Field of View (adjust as needed)
            aspect=float(640)/480, # Aspect Ratio (Width/Height of the image)
            nearVal=0.01, # Near clipping plane
            farVal=100 # Far clipping plane
        )
        # print("droneid: ",droneid)
        camera_world_pos, _ = p.multiplyTransforms(drone_pos, drone_ori, CAMERA_OFFSET_POS, [0, 0, 0, 1])
        camera_world_target, _ = p.multiplyTransforms(drone_pos, drone_ori, CAMERA_OFFSET_TARGET, [0, 0, 0, 1])

        viewMatrix = p.computeViewMatrix(
            cameraEyePosition=camera_world_pos,
            cameraTargetPosition=camera_world_target,
            cameraUpVector=CAMERA_UP_VECTOR # Using a world-aligned up vector
        )

        # Get camera image
        width, height, rgb_img, depth_img, seg_img = p.getCameraImage( width=224, height=224, viewMatrix=viewMatrix,projectionMatrix=projectionMatrix, renderer=p.ER_BULLET_HARDWARE_OPENGL)

        p.configureDebugVisualizer(p.COV_ENABLE_SEGMENTATION_MARK_PREVIEW, 1)
        p.configureDebugVisualizer(p.COV_ENABLE_DEPTH_BUFFER_PREVIEW, 1)
        p.configureDebugVisualizer(p.COV_ENABLE_RGB_BUFFER_PREVIEW, 1)
    
        if USING_GYMNASIUM:
            return obs, reward, terminated, truncated, info
        else:
            return obs, reward, terminated, info

# Environment creator
def make_enhanced_env(follow_camera=False, control_mode="position_yaw"):
    base_env = CtrlAviary(
        drone_model=DroneModel.CF2X,
        num_drones=1,
        physics=Physics.PYB,
        gui=True,
        record=False
    )

    return EnhancedPPOPIDHybridWrapper(base_env, follow_camera=follow_camera, control_mode=control_mode)

if __name__ == "__main__":
    print("🚁 EMERGENCY TAKEOFF FIX + WORKING CRASH RECOVERY ACTIVATED")
    print("This version will:")
    print("- FORCE the drone to climb for the first 500 steps")
    print("- ACTUALLY DETECT crashes (ground contact or flipping)")  
    print("- AUTOMATICALLY reset drone to upright position when crashed")
    print("- RESTART takeoff sequence after crash recovery")
    
    CONTROL_MODE = "position_yaw"
    
    vec_env = make_vec_env(lambda: make_enhanced_env(follow_camera=False, control_mode=CONTROL_MODE), n_envs=1)
    
    model = PPO(
        "MlpPolicy", 
        vec_env, 
        verbose=1,
        tensorboard_log="./ppo_pid_emergency_logs/",
        learning_rate=0.005,  # Lower learning rate for stability
        n_steps=1024,         # Smaller steps for faster feedback
        batch_size=32,        # Smaller batch
        n_epochs=10,
        gamma=0.99,           # Standard gamma
        gae_lambda=0.95,
        clip_range=0.2,
        ent_coef=0.01
    )
    
    print(f"🎯 Emergency training with forced takeoff and WORKING crash recovery...")
    print(f"Action space: {vec_env.action_space}")
    
    try:
        model.learn(total_timesteps=50000)  # Shorter training to test fix
        model.save(f"ppo_emergency_takeoff_recovery_working_{CONTROL_MODE}")
        print(f"✅ Model saved as 'ppo_emergency_takeoff_recovery_working_{CONTROL_MODE}'")
    except KeyboardInterrupt:
        print("🛑 Training interrupted by user")
        model.save(f"ppo_emergency_takeoff_recovery_working_{CONTROL_MODE}_interrupted")
    finally:
        vec_env.close()
        print("🏁 Emergency training with WORKING crash recovery completed!")