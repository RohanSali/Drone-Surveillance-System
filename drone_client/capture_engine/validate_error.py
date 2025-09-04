import cv2
from ultralytics import YOLO
import os
import sys
import json
from datetime import datetime
import base64
import asyncio
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))
from inference_engine.result_saver import send_alert

current_dir = os.path.dirname(os.path.abspath(__file__))
project_dir = os.path.abspath(os.path.join(current_dir, '..'))
ALERT_MODEL_PATH = os.path.join(project_dir,'models','yolo_alert_detection.pt')
det_model_alert = YOLO(ALERT_MODEL_PATH)
CROWD_MODEL_PATH = os.path.join(project_dir,'models','crowd_density_colab.pt')
det_model_crowd = YOLO(CROWD_MODEL_PATH)

CASULTIES = ['Accident Detected', 'Fire Detected', 'Flood Detected', 'Structural Damage Detected']
ANAMOLIES = ['Blood Detected', 'Face Mask Detected', 'Gun Detected','Knife Detected']
CROWD_DENSITY = ['Low Density', 'Medium Density', 'High Density']
CONFIDENCE_THRESHOLD = 0.25

DRONE_INFO_FILE_PATH = os.path.join(project_dir, 'drone_info.json')
drone_info = {}
with open(DRONE_INFO_FILE_PATH,'r') as f:
    drone_info=json.load(f)

def create_required_files_and_folders():
    validated_alerts_file = os.path.join(current_dir,'validated_alerts.txt')
    drone_targets_file = os.path.join(current_dir,'drone_targets.txt')

    # Create the file if it doesn't exist
    if not os.path.exists(validated_alerts_file):
        with open(validated_alerts_file, 'w') as f:
            f.write('')  # Or write default content
        print(f"Created file: {validated_alerts_file}")
    if not os.path.exists(drone_targets_file):
        with open(drone_targets_file, 'w') as f:
            f.write('')  # Or write default content
        print(f"Created file: {drone_targets_file}")

create_required_files_and_folders()

TEMP_TEXT_FILE = os.path.join(current_dir,'validated_alerts.txt')

def detect_object(frame, det_model, alert_name):
    print(f"🔍 Detecting {alert_name} in the frame...")
    results = det_model(frame, conf=CONFIDENCE_THRESHOLD)[0]  # batch size 1

    # Normalize alert names
    name_map = {
        'Fire Detected': 'fire',
        'Flood Detected': 'flood',
        'Accident Detected': 'accident',
        'Blood Detected': 'blood',
        'Face Mask Detected': 'face_mask',
        'Gun Detected': 'gun',
        'Knife Detected': 'knife',
        'Structural Damage Detected': 'collapse',
    }
    alert_name = name_map.get(alert_name, 'no_alert')

    # Get model class names
    class_names = det_model.names  # dict {0: 'fire', 1: 'flood', ...}

    # Find class ID for the alert_name
    casualty_class_id = None
    for cls_id, cls_name in class_names.items():
        if cls_name == alert_name:
            casualty_class_id = cls_id
            break

    if casualty_class_id is None:
        print(f"[ERROR] Class '{alert_name}' not found in model.")
        return frame,0,False  # Return original frame without modifications

    # Draw all bounding boxes for that alert
    max_score = 0.0
    for box in results.boxes:
        if int(box.cls) == casualty_class_id:
            x1, y1, x2, y2 = map(int, box.xyxy[0].cpu().numpy())
            score = float(box.conf[0])  # Confidence score
            max_score = max(max_score, score)

            # Draw rectangle
            cv2.rectangle(frame, (x1, y1), (x2, y2), (0, 255, 0), 2)

            # Label text
            label = f"{alert_name} {score:.2f}"
            cv2.putText(frame, label, (x1, y1 - 10),cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)

    return frame,max_score,True

def encode_frame(frame):
    # Encode the frame to JPEG format
    success, encoded_image = cv2.imencode('.jpg', frame)
    if not success:
        raise ValueError("Image encoding failed")

    # Convert to base64
    base64_image = base64.b64encode(encoded_image).decode('utf-8')
    return base64_image

def save_to_machine(payload):
    with open(TEMP_TEXT_FILE, 'a') as f:
        f.write(str(payload) + '\n')
    print(f"✅ Saved Validated Alert to Machine : {payload['alert']}")

def validate_alert(alert_name,alert_id,frame,capture_timestamp = datetime.now()):
    type = None
    if alert_name in CASULTIES or alert_name in ANAMOLIES:
        det_model = det_model_alert
        type = 'Casualty'
    elif alert_name in ANAMOLIES:
        det_model = det_model_alert
        type = 'Anamoly'
    elif any(alert_name.startswith(prefix) for prefix in CROWD_DENSITY):
        det_model = det_model_crowd
        type = 'Crowd'
    else:
        print(f"Unknown alert type: {alert_name}")
        return False
    
    frame,prob,detected = detect_object(frame, det_model, alert_name)

    if detected:
        frame_blob = encode_frame(frame)
        payload = {
            "alert_id" : alert_id,
            "alert" : type + " - " + alert_name,
            "drone_id" : drone_info.get('drone_id','NO DRONE'),
            "alert_location" : [0,0,0],
            "image" : frame_blob,
            "image_received" : 1,
            "rl_responsed" : 1,
            "score" : round(prob,2),
            "timestamp" : capture_timestamp.isoformat()
        }

        save_to_machine(payload)
        asyncio.run(send_alert(payload,type='validated_alert'))