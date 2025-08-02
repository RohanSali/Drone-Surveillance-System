import os
import cv2
import threading
import queue
from datetime import datetime
from pathlib import Path
from casulty_inference import inference_casulty
from anamoly_inference import inference_anamoly
from face_recognition import compare_faces
from crowd_density import inference_crowd_density
import asyncio
from alert_image_handler import start_alert_image_handler

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

LOST_FINDING_IMG_PATH = Path(os.path.join(base_dir, 'lost_person\image1.jpg'))

# Define 1-slot queues for real-time inference (latest frame only)
casulty_queue = queue.Queue(maxsize=1)
anamoly_queue = queue.Queue(maxsize=1)
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

# Async inference thread for casulty
def casulty_worker():
    while True:
        frame, timestamp = casulty_queue.get()
        inference_casulty(frame, timestamp)
        casulty_queue.task_done()

# Async inference thread for anomaly
def anamoly_worker():
    while True:
        frame, timestamp = anamoly_queue.get()
        inference_anamoly(frame, timestamp)
        anamoly_queue.task_done()

# Async inference thread for face recognition
def match_face_worker():
    while True:
        frame2 = cv2.imread(str(LOST_FINDING_IMG_PATH))
        
        if frame2 is None:
            # No image found, continue waiting
            import time
            time.sleep(1)  # Wait a bit before checking again
            continue
        
        frame, timestamp = match_face_queue.get()
        compare_faces(frame, frame2, LOST_FINDING_IMG_PATH.stem , timestamp)
        match_face_queue.task_done()

# Async inference thread for crowd density
def crowd_density_worker():
    while True:
        frame, timestamp = crowd_queue.get()
        inference_crowd_density(frame, timestamp)
        crowd_queue.task_done()

# WebSocket alert image handler thread
def alert_image_handler_worker():
    """Run the alert image handler in a separate thread"""
    loop = asyncio.new_event_loop()
    asyncio.set_event_loop(loop)
    try:
        loop.run_until_complete(start_alert_image_handler())
    except Exception as e:
        print(f"❌ Alert image handler error: {e}")
    finally:
        loop.close()

# Start all threads as daemons
threading.Thread(target=casulty_worker, daemon=True).start()
threading.Thread(target=anamoly_worker, daemon=True).start()
threading.Thread(target=match_face_worker, daemon=True).start()
threading.Thread(target=crowd_density_worker, daemon=True).start()
threading.Thread(target=alert_image_handler_worker, daemon=True).start()

print("🚀 All inference threads started including alert image handler")

def runner_app(device="lap",img=None):
    if device == "lap":
        cap = cv2.VideoCapture(0)
    elif device == "cam":
        cap = cv2.VideoCapture(1)
    else :
        cap = None

    while True:
        if cap:
            ret, frame = cap.read()
        else:
            ret = False
            frame = None

        timestamp = datetime.now()

        if not ret:
            break

        # Send to inference queues (copy frame to avoid race condition)
        safe_put(casulty_queue, (frame.copy(), timestamp))
        safe_put(anamoly_queue, (frame.copy(), timestamp))
        safe_put(match_face_queue, (frame.copy(), timestamp))
        safe_put(crowd_queue, (frame.copy(), timestamp))

        # Display live feed
        cv2.imshow("Live Feed", frame)
        if cv2.waitKey(1) == ord('q'):
            break

    cap.release()
    cv2.destroyAllWindows()

def runner_sim(img_array=None):
    frame = None
    if img_array is not None:
        frame = cv2.cvtColor(img_array,cv2.COLOR_RGB2BGR)

    if frame is None:
        return
    
    timestamp = datetime.now()

    # Send to inference queues (copy frame to avoid race condition)
    safe_put(casulty_queue, (frame.copy(), timestamp))
    safe_put(anamoly_queue, (frame.copy(), timestamp))
    safe_put(match_face_queue, (frame.copy(), timestamp))
    safe_put(crowd_queue, (frame.copy(), timestamp))

    # Display live feed
    cv2.imshow("Live Feed", frame)
    if cv2.waitKey(1) == ord('q'):
        cv2.destroyAllWindows()

cv2.destroyAllWindows()