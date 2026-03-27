import asyncio
import os
import sys
if sys.platform == 'win32':
    asyncio.set_event_loop_policy(asyncio.WindowsSelectorEventLoopPolicy())

import airsim
import threading
import math
import time
import ast
import numpy as np
from datetime import datetime
import cv2
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..')))
# from drone_client.inference_engine.temp_my import test_sim
from drone_client.inference_engine import runner
from drone_client.capture_engine import validate_error

current_dir = os.path.dirname(os.path.abspath(__file__))
project_dir = os.path.abspath(os.path.join(current_dir, '..', '..'))

DRONE_NAME = "Drone1"
DRONE_CORDS_PATH = os.path.join(project_dir,"drone_client","capture_engine","drone_targets.txt")
CHECK_INTERVAL = 2.0  # seconds between file checks
VELOCITY = 5  # drone movement speed (m/s)
FOV = 90  # camera field of view in degrees
HOVER_STABILIZE_TIME = 2.0  # seconds to hover for stabilization

# --- State flags (thread-safe via GIL for simple booleans) ---
drone_state = "IDLE"  # "IDLE", "MOVING", "HOVERING", "AT_LOCATION"
is_validating = False
running = False

# --- Single AirSim client used ONLY from the main thread ---
client = airsim.MultirotorClient()
client.confirmConnection()
client.enableApiControl(True, DRONE_NAME)
client.armDisarm(True, DRONE_NAME)

def get_intrinsic_matrix(width, height, fov_deg = FOV):
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
    qw, qx, qy, qz = drone_ori.w_val, drone_ori.x_val, drone_ori.y_val, drone_ori.z_val
    return np.array([
        [1 - 2*qy**2 - 2*qz**2, 2*qx*qy - 2*qz*qw, 2*qx*qz + 2*qy*qw],
        [2*qx*qy + 2*qz*qw, 1 - 2*qx**2 - 2*qz**2, 2*qy*qz - 2*qx*qw],
        [2*qx*qz - 2*qy*qw, 2*qy*qz + 2*qx*qw, 1 - 2*qx**2 - 2*qy**2]
    ])

def quaternion_to_euler(q):
    # Convert AirSim quaternion to Euler angles (in degrees)
    w, x, y, z = q.w_val, q.x_val, q.y_val, q.z_val

    # Roll (x-axis rotation)
    sinr_cosp = 2 * (w * x + y * z)
    cosr_cosp = 1 - 2 * (x * x + y * y)
    roll = math.degrees(math.atan2(sinr_cosp, cosr_cosp))

    # Pitch (y-axis rotation)
    sinp = 2 * (w * y - z * x)
    if abs(sinp) >= 1:
        pitch = math.degrees(math.copysign(math.pi / 2, sinp))
    else:
        pitch = math.degrees(math.asin(sinp))

    # Yaw (z-axis rotation)
    siny_cosp = 2 * (w * z + x * y)
    cosy_cosp = 1 - 2 * (y * y + z * z)
    yaw = math.degrees(math.atan2(siny_cosp, cosy_cosp))

    return [roll, pitch, yaw]

def get_drone_position_and_orientation():
    pose = client.simGetVehiclePose(vehicle_name=DRONE_NAME)
    position = [pose.position.x_val, pose.position.y_val, pose.position.z_val]
    orientation = pose.orientation
    orientation_euler = quaternion_to_euler(orientation)
    return position, orientation, orientation_euler

def load_coordinates_from_file(file_path=DRONE_CORDS_PATH):
    """Reads new coordinates from file, removes processed ones."""
    position = []
    lines_to_keep = []

    try:
        if not os.path.exists(file_path):
            open(file_path, 'w').close()  # Create empty file if not exists
            return position

        with open(file_path, 'r') as file:
            lines = file.readlines()

        for line_num, line in enumerate(lines, 1):
            try:
                line = ast.literal_eval(line.strip())  # Convert string → dict safely
                if isinstance(line, dict) and 'target_id' in line and 'location' in line:
                    target_id = line.get('target_id')
                    coords = line.get('location')
                    if len(coords) >= 4:
                        x, y, z, yaw = coords
                        alert_name = line.get('alert_name', '')
                        alert_id = line.get('alert_id', '')
                        position.append([f"target_{target_id}", x, y, z, yaw, alert_name, alert_id])
                        print(f"📍 Loaded target_{target_id} → ({x}, {y}, {z}, {yaw}°)")
                    else:
                        lines_to_keep.append(str(line) + '\n')
                else:
                    lines_to_keep.append(str(line) + '\n')
            except Exception as e:
                print(f"⚠️ Invalid line {line_num}: {line} | error: {e}")
                lines_to_keep.append(str(line) + '\n')

        # Keep only unprocessed lines
        with open(file_path, 'w') as file:
            file.writelines(lines_to_keep)

    except Exception as e:
        print(f"❌ Error reading file: {e}")

    return position

def getImgFrame():
    """Capture a single frame from the AirSim camera. Must be called from main thread."""
    try:
        response = client.simGetImages([airsim.ImageRequest("0", airsim.ImageType.Scene, False, False)])[0]

        if response.height == 0 or response.width == 0 or response.image_data_uint8 is None:
            return None, None, None, None, None

        # Convert to numpy array
        img1d = np.frombuffer(response.image_data_uint8, dtype=np.uint8)

        # Make sure we reshape according to the actual height & width
        expected_size = response.height * response.width * 3
        if img1d.size != expected_size:
            print(f"⚠ Warning: image size mismatch! Got {img1d.size}, expected {expected_size}")
            return None, None, None, None, None

        # Reshape
        img_rgba = img1d.reshape((response.height, response.width, 3))

        # Convert BGR → RGB
        rgb_array = img_rgba[:, :, :3][:, :, ::-1]
        height, width, _ = rgb_array.shape

        # Get drone pose
        drone_pos, drone_ori, drone_ori_euler = get_drone_position_and_orientation()

        intrinsic_matrix = get_intrinsic_matrix(width, height, fov_deg=FOV)
        rotation_matrix = get_drone_rotation_matrix(drone_ori)

        return rgb_array, intrinsic_matrix, rotation_matrix, width, height

    except Exception as e:
        print("Error capturing image:", e)
        return None, None, None, None, None

def execute_coordinate_queue(coords):
    """
    Execute a list of target coordinates sequentially.
    MUST be called from the main thread (uses the global AirSim client).
    """
    global drone_state, is_validating

    for i, coord in enumerate(coords):
        if not running:
            break
        name, x, y, z, yaw, alert_name, alert_id = coord
        print(f"\n🎯 Going to position {i+1}/{len(coords)}: {name}")
        print(f"   → Coordinates: ({x}, {y}, {z}) | Yaw: {yaw}°")

        drone_state = "MOVING"

        # Move to target position
        client.moveToPositionAsync(
            x, y, z, VELOCITY,
            yaw_mode=airsim.YawMode(is_rate=False, yaw_or_rate=yaw),
            vehicle_name=DRONE_NAME
        ).join()

        if not running:
            break

        # Hover to stabilize
        drone_state = "HOVERING"
        print("🚁 Hovering to stabilize...")
        client.hoverAsync(vehicle_name=DRONE_NAME).join()
        time.sleep(HOVER_STABILIZE_TIME)

        if not running:
            break

        # Capture frame and validate alert
        is_validating = True
        frame, intrinsic_matrix, rotation_matrix, w, h = getImgFrame()
        if frame is not None:
            validate_error.validate_alert(alert_name, alert_id, frame, datetime.now())
        is_validating = False

        print(f"✅ Reached {name}")
        print(f"   [Alert Name: {alert_name} | ID: {alert_id}] @ {datetime.now().strftime('%H:%M:%S')}")

        drone_state = "AT_LOCATION"
        time.sleep(0.5)


def file_monitor_thread():
    """
    Background thread that ONLY watches the file for new coordinates.
    Does NOT call any AirSim APIs. Puts new coords into the shared queue.
    """
    print("🔄 Monitoring for new coordinates...")
    print("💡 Add new lines to drone_targets.txt — they'll be executed automatically!")
    while running:
        try:
            new_coords = load_coordinates_from_file(DRONE_CORDS_PATH)
            if new_coords:
                print(f"🆕 Found {len(new_coords)} new coordinate(s). Adding to queue...")
                for c in new_coords:
                    coord_queue.append(c)
            time.sleep(CHECK_INTERVAL)
        except Exception as e:
            print(f"⚠️ Error monitoring file: {e}")
            time.sleep(2)


# Shared coordinate queue — appended by file_monitor_thread, consumed by main thread
coord_queue = []


def main():
    global running, drone_state

    running = True

    # Takeoff
    print("🚁 Taking off...")
    client.takeoffAsync(vehicle_name=DRONE_NAME).join()
    print("✅ Drone ready for mission!\n")

    drone_state = "AT_LOCATION"

    # Load initial coordinates
    initial_coords = load_coordinates_from_file(DRONE_CORDS_PATH)
    if initial_coords:
        print(f"📋 Executing {len(initial_coords)} initial coordinates...")
        execute_coordinate_queue(initial_coords)
        print("🏁 Initial targets complete.\n")

    # Start file monitor (only does file I/O, NO AirSim calls)
    threading.Thread(target=file_monitor_thread, daemon=True).start()

    print("📷 Continuous inference active.")
    print("💡 Press Ctrl+C to stop the mission.")

    try:
        while running:
            # --- 1. Check for new target coordinates from the file monitor ---
            if coord_queue:
                batch = list(coord_queue)
                coord_queue.clear()
                print(f"\n🆕 Executing {len(batch)} new coordinate(s)...")
                execute_coordinate_queue(batch)
                print("✅ Completed new coordinates.\n")

            # --- 2. Continuous inference (when NOT serving targets) ---
            if drone_state == "AT_LOCATION" and not is_validating:
                frame, intrinsic_matrix, rotation_matrix, w, h = getImgFrame()
                if frame is not None:
                    drone_pos, _, _ = get_drone_position_and_orientation()
                    runner.runner_sim(frame, intrinsic_matrix, rotation_matrix, drone_pos)

            # Small sleep to keep the loop responsive without hammering AirSim
            time.sleep(0.1)

    except KeyboardInterrupt:
        print("\n🛑 Mission interrupted by user.")
    finally:
        running = False
        print("🛬 Landing drone...")
        try:
            client.landAsync(vehicle_name=DRONE_NAME).join()
        except Exception as e:
            print(f"⚠ Landing error: {e}")
        client.armDisarm(False, DRONE_NAME)
        client.enableApiControl(False, DRONE_NAME)
        print("✅ Drone disconnected from AirSim.")

if __name__ == "__main__":
    main()

