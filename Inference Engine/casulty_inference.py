import cv2
import numpy as np
import tensorflow as tf
from tensorflow.keras.models import load_model
from tensorflow.keras.preprocessing import image
import json
from datetime import datetime
import ast
from collections import deque
import requests

IMG_HEIGHT = 380
IMG_WIDTH = 380
MODEL_PATH = 'models\casualty_classifier.h5'
TEMP_TEXT_FILE = 'Inference Engine/alerts.txt'
PREDICTION_THRESHOLD = 93 #percent
TIME_THRESHOLD = 60 #sec
URL = "http://web-production-190fc.up.railway.app/api/alerts"
class_names = ['Accident Detected', 'Fire Detected', 'Flood Detected', 'No Casualty']
model = load_model(MODEL_PATH)

def preprocess_frame(frame):
    frame_resized = cv2.resize(frame, (IMG_WIDTH, IMG_HEIGHT))
    frame_rgb = cv2.cvtColor(frame_resized, cv2.COLOR_BGR2RGB)

    img_array = image.img_to_array(frame_rgb)
    img_array_expanded = np.expand_dims(img_array, axis=0)
    img_preprocessed = tf.keras.applications.efficientnet.preprocess_input(img_array_expanded)

    return img_preprocessed


def classify_frame(frame):
    processed_frame = preprocess_frame(frame)

    pred_probs = model.predict(processed_frame,verbose=0)

    pred_class_idx = np.argmax(pred_probs, axis=1)[0]
    pred_class_name = class_names[pred_class_idx]    

    return pred_class_name , pred_probs[0,pred_class_idx] * 100

def send_to_server(payload):
    response = requests.post(URL,json=payload)
    print("Response : ",response)
    # json.dumps(payload)
    if response.ok :
        print(f"✅ Sent to server as {payload['alert']}")

def save_to_machine(payload):
    with open(TEMP_TEXT_FILE, 'a') as f:
        f.write(str(payload) + '\n')
    print(f"✅ Saved to machine as {payload['alert']}")


def inference_casulty(frame , capture_timestamp):
    label , prob = classify_frame(frame)
    processed_timestamp = datetime.now()
    processing_duration = (processed_timestamp - capture_timestamp).total_seconds()

    if prob < PREDICTION_THRESHOLD :
        label = 'No Casualty'

    if label != 'No Casualty':
        payload = {
            "alert" : "Casualty - " + label,
            "drone_id" : "NO DRONE",
            "alert_location" : (0,0,0),
            "image" : None,
            "image_received" : 0,
            "rl_responsed" : 0,
            "score" : round(prob,2),
            "timestamp" : capture_timestamp.isoformat()
        }
        
        timestamp = None
        new_label = "Casualty - " + label
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
                save_to_machine(payload)
                send_to_server(payload)
        else :
            print(f"🔒 Saving {new_label} for first time!")
            save_to_machine(payload)
            send_to_server(payload)

        

    print(f"▶️  Frame processed in time {processing_duration:.3f} seconds & label : {label}")