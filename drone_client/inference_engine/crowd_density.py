import os
import sys
import ast
import asyncio
import json
import cv2
import numpy as np
import torch
from ultralytics import YOLO
from collections import deque
from datetime import datetime
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from result_saver import send_alert , save_to_machine

current_dir = os.path.dirname(os.path.abspath(__file__))
project_dir = os.path.abspath(os.path.join(current_dir, '..'))

MODEL_PATH = os.path.join(project_dir,'models','crowd_density_colab.pt')
TEMP_TEXT_FILE = os.path.join(current_dir,'alerts.txt')
CONFIDENCE = 0.209 #confidence threshold
TIME_THRESHOLD = 60*3 #sec
DRONE_INFO_FILE_PATH = os.path.join(project_dir, 'drone_info.json')
drone_info = {}
with open(DRONE_INFO_FILE_PATH,'r') as f:
    drone_info=json.load(f)

model = YOLO(MODEL_PATH) 
device = 'cuda' if torch.cuda.is_available() else 'cpu'
midas_model = torch.hub.load("intel-isl/MiDaS", "MiDaS_small")
midas_model.to(device)
midas_model.eval()
midas_transform = torch.hub.load("intel-isl/MiDaS", "transforms").small_transform

COUNT_CHART = {
    0 :'No Person',
    1 : 'Single Person',
    2 : 'Two to Four People',
    3 : 'Five to Ten People',
    4 : 'Ten+ to Fifty People',
    5 : 'Fifty+ to Hundred People',
    6 : 'Hundered+ to Two Hundred People',
    7 : 'Two Hundered+ to Five Hundred People',
    8 : 'Five Hundered+ to Thousand People',
    9 : 'Several Thousand People',
    10 : 'Ten Thousand+'
    }
DENSITY_CHART = {
    1 : 'Low Density',
    2 : 'Low Density',
    3 : 'Low Density',
    4 : 'Medium Density',
    5 : 'Medium Density',
    6 : 'Medium Density',
    7 : 'High Desnsity',
    8 : 'High Desnsity',
    9 : 'High Desnsity'
}

def get_idx(number_of_people):
    idx = 0
    if number_of_people <= 0:
        idx = 0
    elif number_of_people == 1 :
        idx = 1
    elif number_of_people > 1 and number_of_people <= 4 :
        idx = 2
    elif number_of_people > 4 and number_of_people <= 10 :
        idx = 3
    elif number_of_people > 10 and number_of_people <= 50 :
        idx = 4
    elif number_of_people > 50 and number_of_people <= 100 :
        idx = 5
    elif number_of_people > 100 and number_of_people <= 200 :
        idx = 6
    elif number_of_people > 200 and number_of_people <= 500 :
        idx = 7
    elif number_of_people > 500 and number_of_people <= 1000 :
        idx = 8
    elif number_of_people > 1000 and number_of_people <= 10000 :
        idx = 9
    else :
        idx = 10
    return idx

def get_avg_centers(boxes):
    if len(boxes) > 0:
        centers = []
        for box in boxes.xyxy:  # each box in [x1, y1, x2, y2] format
            x1, y1, x2, y2 = box
            cx = (x1 + x2) / 2
            cy = (y1 + y2) / 2
            centers.append((cx.item(), cy.item()))  # convert to Python floats

        if len(centers) == 1:
            avg_x, avg_y = centers[0]  # single face → center of that box
        else:
            avg_x = sum(c[0] for c in centers) / len(centers)
            avg_y = sum(c[1] for c in centers) / len(centers)

        print(f"Average center: ({avg_x:.2f}, {avg_y:.2f})")
    else:
        print("No faces detected")

    return int(avg_x), int(avg_y)

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

def get_crowd_location(frame,result,intrinsic_matrix,drone_rotation_matrix,drone_pos):
    x_img , y_img = get_avg_centers(result[0].boxes)

    if x_img is not None and y_img is not None :
        z_img = get_depth(frame, x_img, y_img)
        camera_coordinates = pixel_to_camera_coordinates(x_img, y_img, z_img, intrinsic_matrix)
        if camera_coordinates is not None:
            drone_pos = np.array(drone_pos)
            x2, y2, z2 = get_alert_pos_coordinates(camera_coordinates, drone_pos ,drone_rotation_matrix)

            return x2, y2, z2
        
    return None, None, None


def inference_crowd_density(frame,capture_timestamp, intrinsic_matrix , drone_rotation_matrix, drone_pos= [0,0.2,5]):
    result = model(frame, conf=CONFIDENCE ,verbose=False)
    number_of_people = len(result[0].boxes)

    idx = get_idx(number_of_people)
    
    new_label = ""

    if idx == 0 or idx == 10 :
        new_label = "Out of range - " + COUNT_CHART[idx]
    else :
        new_label = DENSITY_CHART[idx] + " - " + COUNT_CHART[idx]

    if new_label.startswith("Out of range - "):
        pass
    else : 
        payload = {
            "alert" : new_label,
            "drone_id" : drone_info.get('drone_id','NO DRONE'),
            "alert_location" : [0,0,0],
            "image" : None,
            "image_received" : 0,
            "rl_responsed" : 0,
            "score" : 0,
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

        if timestamp :
            time_difference = (capture_timestamp - timestamp).total_seconds()
            print(f"Time difference between last {new_label} prediction is : {time_difference} ⏳")
            if time_difference > TIME_THRESHOLD:
                x,y,z = get_crowd_location(frame,result,intrinsic_matrix,drone_rotation_matrix,drone_pos)
                if x is not None and y is not None and z is not None :
                    payload["alert_location"] = [x, y, z]
                    
                asyncio.run(save_to_machine(payload))
                asyncio.run(send_alert(payload))
                
        else :
            print(f"🔒 Saving {new_label} for first time!")

            x,y,z = get_crowd_location(frame,result,intrinsic_matrix,drone_rotation_matrix,drone_pos)
            if x is not None and y is not None and z is not None :
                payload["alert_location"] = [x, y, z]

            asyncio.run(save_to_machine(payload))
            asyncio.run(send_alert(payload))

    print(f"Found {number_of_people} peoples in the frame!")