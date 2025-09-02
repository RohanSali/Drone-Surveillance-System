import os
import cv2
import threading
import queue
from datetime import datetime
from pathlib import Path
import numpy as np
import sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from alert_inference import inference_for_alert
from face_recognition import compare_faces
from crowd_density import inference_crowd_density

base_dir = os.path.dirname(os.path.abspath(__file__))

def create_required_files_and_folders():
    lost_person_folder = os.path.join(base_dir, 'lost_person')
    alerts_file = os.path.join(base_dir,'alerts.txt')
    person_found_file = os.path.join(base_dir,'person_found.txt')

    # Create the folder if it doesn't exist
    if not os.path.exists(lost_person_folder):
        os.makedirs(lost_person_folder)
        print(f"Created folder: {lost_person_folder}")

    # Create the file if it doesn't exist
    if not os.path.exists(alerts_file):
        with open(alerts_file, 'w') as f:
            f.write('')  # Or write default content
        print(f"Created file: {alerts_file}")
    
    if not os.path.exists(person_found_file):
        with open(person_found_file, 'w') as f:
            f.write('')  # Or write default content
        print(f"Created file: {person_found_file}")

create_required_files_and_folders()

LOST_FINDING_IMG_FOLDER = Path(os.path.join(base_dir, 'lost_person'))

# Define 1-slot queues for real-time inference (latest frame only)
alert_queue = queue.Queue(maxsize=1)
match_face_queue = queue.Queue(maxsize=1)
crowd_queue = queue.Queue(maxsize=1)

# Function to safely insert latest frame into a queue (replaces old)
def safe_put(q, item):
    if q.full():
        try:
            q.get_nowait()
        except queue.Empty:
            pass
    q.put(item)

# Async inference thread for alert
def alert_worker():
    while True:
        frame, timestamp, intrinsic_matrix, drone_rotation_matrix, drone_pos = alert_queue.get()
        inference_for_alert(frame, timestamp , intrinsic_matrix, drone_rotation_matrix, drone_pos)
        alert_queue.task_done()

# Async inference thread for face recognition
def match_face_worker():
    while True:
        image_extensions = ('.jpg', '.jpeg', '.png', '.bmp', '.tiff')
        image_files = [f for f in os.listdir(LOST_FINDING_IMG_FOLDER) if f.lower().endswith(image_extensions)]

        count = 0
        # Sort (optional) and loop
        for image_file in sorted(image_files):
            LOST_FINDING_IMG_PATH = os.path.join(LOST_FINDING_IMG_FOLDER, image_file)
            image = cv2.imread(LOST_FINDING_IMG_PATH)

            if image is not None:
                print(f"Loaded: {image_file}, shape: {image.shape}")
                frame2 = image.copy()
                frame, timestamp, intrinsic_matrix, drone_rotation_matrix, drone_pos= match_face_queue.get()
                count+=1
                compare_faces(frame, frame2, image_file, timestamp, intrinsic_matrix, drone_rotation_matrix, drone_pos)
            else:
                print(f"Failed to load: {image_file}")

            for i in range(count):
                try:
                    match_face_queue.task_done()
                except queue.Empty:
                    break

# Async inference thread for crowd density
def crowd_density_worker():
    while True:
        frame, timestamp ,intrinsic_matrix, drone_rotation_matrix, drone_pos = crowd_queue.get()
        inference_crowd_density(frame, timestamp ,intrinsic_matrix, drone_rotation_matrix, drone_pos)
        crowd_queue.task_done()

# Start both threads as daemons
threading.Thread(target=alert_worker, daemon=True).start()
threading.Thread(target=match_face_worker, daemon=True).start()
threading.Thread(target=crowd_density_worker, daemon=True).start()


def runner_app(device="lap"):
    if device == "lap":
        cap = cv2.VideoCapture(0)
    elif device == "cam":
        cap = cv2.VideoCapture(1)
    else :
        cap = None

    intrinsic_matrix = np.array([[554.256,   0,     320],
                                 [   0,   554.256 , 240],
                                 [   0,      0,      1]], dtype=np.float32)
    drone_rotation_matrix = np.eye(3) 
    drone_pos = [0, 0, 0] 

    while True:
        if cap:
            ret, frame = cap.read()
            frame = cv2.flip(frame, 1)  # Flip the frame horizontally
        else:
            ret = False
            frame = None

        timestamp = datetime.now()

        if not ret:
            break

        # Send to inference queues (copy frame to avoid race condition)
        safe_put(alert_queue, (frame.copy(), timestamp, intrinsic_matrix, drone_rotation_matrix, drone_pos))
        safe_put(match_face_queue, (frame.copy(), timestamp, intrinsic_matrix, drone_rotation_matrix, drone_pos))
        safe_put(crowd_queue, (frame.copy(), timestamp, intrinsic_matrix, drone_rotation_matrix, drone_pos))

        # Display live feed
        cv2.imshow("Live Feed", frame)
        if cv2.waitKey(1) == ord('q'):
            break

    cap.release()
    cv2.destroyAllWindows()

def runner_sim(rgb_array,intrinsic_matrix,drone_rotation_matrix,drone_pos):
    frame = cv2.cvtColor(rgb_array, cv2.COLOR_RGB2BGR)
    
    timestamp = datetime.now()

    # Send to inference queues (copy frame to avoid race condition)
    safe_put(alert_queue, (frame.copy(), timestamp, intrinsic_matrix, drone_rotation_matrix, drone_pos))
    safe_put(match_face_queue, (frame.copy(), timestamp, intrinsic_matrix, drone_rotation_matrix, drone_pos))
    safe_put(crowd_queue, (frame.copy(), timestamp, intrinsic_matrix, drone_rotation_matrix, drone_pos))

    # Display live feed
    cv2.imshow("Live Feed", frame)

    if cv2.waitKey(1) == ord('q'):
        cv2.destroyAllWindows()

cv2.destroyAllWindows()