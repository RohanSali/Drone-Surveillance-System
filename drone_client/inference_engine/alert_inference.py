import os
import sys
import ast
import asyncio
import json
import numpy as np
import cv2
import tensorflow as tf
from tensorflow.keras.preprocessing import image
from tensorflow.keras.applications import EfficientNetB4
from tensorflow.keras.layers import GlobalAveragePooling2D, Dense, Dropout
from tensorflow.keras.models import Model
import torch
from ultralytics import YOLO
from datetime import datetime
from collections import deque
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from result_saver import send_alert , save_to_machine

current_dir = os.path.dirname(os.path.abspath(__file__))
project_dir = os.path.abspath(os.path.join(current_dir, '..'))

MODEL_PATH = os.path.join(project_dir,'models','alert_classifier.keras')
OBJ_DETECTION_MODEL_PATH = os.path.join(project_dir,'models','yolo_alert_detection.pt')
TEMP_TEXT_FILE = os.path.join(current_dir,'alerts.txt')
DRONE_INFO_FILE_PATH = os.path.join(project_dir,'drone_info.json')
IMG_HEIGHT = 240
IMG_WIDTH = 240
PREDICTION_THRESHOLD = 95 #percent
TIME_THRESHOLD = 60 #sec
CONFIDENCE_THRESHOLD = 0.159 #percent

class_names = ['Accident Detected', 'Blood Detected', 'Structural Damage Detected', 'Face Mask Detected','Fire Detected', 'Flood Detected', 'Gun Detected', 'Knife Detected', 'No Alert']
device = 'cuda' if torch.cuda.is_available() else 'cpu'
drone_info = {}
with open(DRONE_INFO_FILE_PATH,'r') as f:
    drone_info=json.load(f)

def create_model(input_shape=(IMG_HEIGHT, IMG_WIDTH, 3), num_classes=len(class_names), dropout_rate=0.5):  #Do not change parameters
    base_model = EfficientNetB4(weights='imagenet', include_top=False, input_shape=input_shape)
    base_model.trainable = False
    x = base_model.output
    x = GlobalAveragePooling2D()(x)
    x = Dense(512, activation='relu')(x)
    x = Dropout(dropout_rate)(x)
    output = Dense(num_classes, activation='softmax')(x)
    model = Model(inputs=base_model.input, outputs=output)
    return model

classifier_model = create_model()
classifier_model.compile(optimizer=tf.keras.optimizers.Adam(learning_rate=1e-3),
                  loss='categorical_crossentropy',
                  metrics=['accuracy'])
classifier_model.load_weights(MODEL_PATH)

detection_model = YOLO(OBJ_DETECTION_MODEL_PATH)
midas_model = torch.hub.load("intel-isl/MiDaS", "MiDaS_small")
midas_model.to(device)
midas_model.eval()
midas_transform = torch.hub.load("intel-isl/MiDaS", "transforms").small_transform

def preprocess_frame(frame):
    frame_resized = cv2.resize(frame, (IMG_WIDTH, IMG_HEIGHT))
    frame_rgb = cv2.cvtColor(frame_resized, cv2.COLOR_BGR2RGB)

    img_array = image.img_to_array(frame_rgb)
    img_array_expanded = np.expand_dims(img_array, axis=0)
    img_preprocessed = tf.keras.applications.efficientnet.preprocess_input(img_array_expanded)

    return img_preprocessed

def classify_frame(frame):
    processed_frame = preprocess_frame(frame)

    pred_probs = classifier_model.predict(processed_frame,verbose=0)

    pred_class_idx = np.argmax(pred_probs, axis=1)[0]
    pred_class_name = class_names[pred_class_idx]    

    return pred_class_name , pred_probs[0,pred_class_idx] * 100

def detect_object(frame, alert_name):
    # Run inference
    print(f"🔍 Detecting {alert_name} in the frame...")
    results = detection_model(frame,conf=CONFIDENCE_THRESHOLD)[0]  # batch size 1, so take first 
    
    name_map = {
        'Fire Detected': 'fire',
        'Flood Detected': 'flood',
        'Accident Detected': 'accident',
        'Blood Detected': 'blood',
        'Face Mask Detected': 'face_mask',
        'Gun Detected': 'gun',
        'Knife Detected': 'knife',
        'Structural Damage Detected' : 'collapse'
    }
    alert_name = name_map.get(alert_name, 'no_alert')

    # Get class names from model
    class_names = detection_model.names  # dict: {0: 'accident', 1: 'blood', ...}

    # Find the class index for the specified alert_name
    alert_class_id = None
    for cls_id, cls_name in class_names.items():
        if cls_name == alert_name:
            alert_class_id = cls_id
            break

    if alert_class_id is None:
        print(f"[ERROR] Class '{alert_name}' not found in model.")
        return None, None

    # Filter boxes for that class
    alert_boxes = []
    for box in results.boxes:
        if int(box.cls) == alert_class_id:
            x1, y1, x2, y2 = box.xyxy[0].cpu().numpy()
            area = (x2 - x1) * (y2 - y1)
            alert_boxes.append((area, (x1, y1, x2, y2)))

    if not alert_boxes:
        return None, None  # No such object detected

    # Select the biggest box (by area)
    biggest_box = max(alert_boxes, key=lambda b: b[0])[1]  # get (x1, y1, x2, y2)

    x1, y1, x2, y2 = biggest_box
    cx = int((x1 + x2) / 2)
    cy = int((y1 + y2) / 2)
    print(f"▶️ Detected {alert_name} at center: ({cx}, {cy})")
    return cx, cy

def get_depth(image ,x_img, y_img):
    img_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    input_batch = midas_transform(img_rgb).to(device)

    with torch.no_grad():
        prediction = midas_model(input_batch)
        prediction = torch.nn.functional.interpolate(
            prediction.unsqueeze(1),
            size=img_rgb.shape[:2],
            mode="bicubic",
            align_corners=False
        ).squeeze()

    depth_map = prediction.cpu().numpy()
    z_img = depth_map[y_img, x_img]

    return float(z_img)

def pixel_to_camera_coordinates(x_img, y_img, z_img, intrinsic_matrix):
    fx = intrinsic_matrix[0, 0]
    fy = intrinsic_matrix[1, 1]
    cx = intrinsic_matrix[0, 2]
    cy = intrinsic_matrix[1, 2]

    # Convert pixel to normalized coordinates
    X_cam = (x_img - cx) * z_img / fx
    Y_cam = (y_img - cy) * z_img / fy
    Z_cam = z_img

    return np.array([X_cam, Y_cam, Z_cam])

def get_alert_pos_coordinates(camera_coords, drone_position, drone_rotation_matrix):
    world_coords = drone_rotation_matrix @ camera_coords + drone_position
    return world_coords

def get_alert_location(frame,label,intrinsic_matrix,drone_rotation_matrix,drone_pos):
    x_img , y_img = detect_object(frame, label)

    if x_img is not None and y_img is not None :
        z_img = get_depth(frame, x_img, y_img)
        camera_coordinates = pixel_to_camera_coordinates(x_img, y_img, z_img, intrinsic_matrix)
        if camera_coordinates is not None:
            drone_pos = np.array(drone_pos)
            x2, y2, z2 = get_alert_pos_coordinates(camera_coordinates, drone_pos ,drone_rotation_matrix)

            return x2, y2, z2
    
    return None,None,None

def inference_for_alert(frame , capture_timestamp, intrinsic_matrix , drone_rotation_matrix, drone_pos= [0,0.2,5]):
    label , prob = classify_frame(frame)
    processed_timestamp = datetime.now()
    processing_duration = (processed_timestamp - capture_timestamp).total_seconds()

    if prob < PREDICTION_THRESHOLD :
        label = 'No Alert'

    if label != 'No Alert':
        if label in ['Accident Detected', 'Structural Damage Detected', 'Fire Detected', 'Flood Detected'] :
            new_label = "Casualty - " + label
        if label in ['Blood Detected', 'Face Mask Detected', 'Gun Detected', 'Knife Detected'] :
            new_label = "Anamoly - " + label

        payload = {
            "alert" : new_label,
            "drone_id" : drone_info["drone_id"],
            "alert_location" : [0,0,0],
            "image" : None,
            "image_received" : 0,
            "rl_responsed" : 0,
            "score" : round(prob,2),
            "timestamp" : capture_timestamp.isoformat()
        }
        
        timestamp = None
        with open(TEMP_TEXT_FILE, 'r') as file:
            lines = deque(file, maxlen=None)

        for line in reversed(lines):
            try:
                data = ast.literal_eval(line.strip())
                if isinstance(data, dict) and data.get('alert') == new_label:
                    timestamp = datetime.fromisoformat(data.get('timestamp'))
                    break
            except Exception as e:
                print(f"❌ Error parsing line: {line} -> {e}")

        if timestamp:
            time_difference = (capture_timestamp - timestamp).total_seconds()
            print(f"Time difference between last {label} prediction is : {time_difference} ⏳")
            if time_difference > TIME_THRESHOLD:
                x,y,z = get_alert_location(frame,label,intrinsic_matrix,drone_rotation_matrix,drone_pos)
                if x is not None and y is not None and z is not None:
                    payload["alert_location"] = [x, y, z]

                asyncio.run(save_to_machine(payload))
                asyncio.run(send_alert(payload))
        else :
            print(f"🔒 Saving {new_label} for first time!")
            x,y,z = get_alert_location(frame,label,intrinsic_matrix,drone_rotation_matrix,drone_pos)
            if x is not None and y is not None and z is not None:
                payload["alert_location"] = [x, y, z]
                    
            asyncio.run(save_to_machine(payload))
            asyncio.run(send_alert(payload))

    print(f"▶️ Frame processed in time {processing_duration:.3f} seconds & label : {label}")