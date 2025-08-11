import torch
import os
from facenet_pytorch import InceptionResnetV1, MTCNN
from PIL import Image
import cv2
import ast
import base64
import numpy as np
from collections import deque
from datetime import datetime
import sys
sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from websocket_call import send_alert
import asyncio
import torch

current_dir = os.path.dirname(os.path.abspath(__file__))
project_dir = os.path.abspath(os.path.join(current_dir, '..'))

TEMP_TEXT_FILE = os.path.join(current_dir,"person_found.txt")
TIME_THRESHOLD = 120 #sec

# Load pretrained FaceNet model
model = InceptionResnetV1(pretrained='vggface2').eval()
mtcnn = MTCNN(image_size=160, margin=0)
device = 'cuda' if torch.cuda.is_available() else 'cpu'
midas_model = torch.hub.load("intel-isl/MiDaS", "MiDaS_small")
midas_model.to(device)
midas_model.eval()
midas_transform = torch.hub.load("intel-isl/MiDaS", "transforms").small_transform


def get_embedding_from_frame(frame):
    # Convert to RGB PIL Image
    img = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))

    boxes, probs = mtcnn.detect(img)
    if boxes is not None and len(boxes) > 0:
        # Get the first detected face
        box = boxes[0]
        x1, y1, x2, y2 = map(int, box)
        
        face = mtcnn(img)
        if face is not None:
            embedding = model(face.unsqueeze(0))
            return embedding,(x1, y1, x2, y2)
        
    return None, None

def encode_frame(frame):
    # Encode the frame to JPEG format
    success, encoded_image = cv2.imencode('.jpg', frame)
    if not success:
        raise ValueError("Image encoding failed")

    # Convert to base64
    base64_image = base64.b64encode(encoded_image).decode('utf-8')
    return base64_image

def get_middle_point(box,name):
    x1, y1, x2, y2 = box
    cx = int((x1 + x2) / 2)
    cy = int((y1 + y2) / 2)
    print(f"▶️ Detected {name} at center: ({cx}, {cy})")
    return cx, cy  

def get_depth(image, x_img, y_img):
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

def save_to_machine(payload):
    with open(TEMP_TEXT_FILE, 'a') as f:
        f.write(str(payload) + '\n')
    print(f"✅ Saved to machine as {payload['name']}")

def compare_faces(frame1, frame2, name, capture_timestamp, intrinsic_matrix, drone_rotation_matrix, drone_pos= [0,0.2,5]):
    emb1 , box = get_embedding_from_frame(frame1)
    emb2 , _ = get_embedding_from_frame(frame2)

    if emb1 is not None and emb2 is not None:
        cosine_sim = torch.nn.functional.cosine_similarity(emb1, emb2).item()
        threshold = 0.5
        if cosine_sim > threshold:
            print("Faces match! Similarity:", cosine_sim , "Person found at timestamp : ",capture_timestamp)

            frame1_blob = encode_frame(frame1)
            frame2_blob = encode_frame(frame2)
            
            # Create payload for saving to machine
            payload = {
                "found":1,
                "name":name,
                "drone_id": "drone_001",
                "actual_image": frame2_blob,
                "matched_frame" : frame1_blob,
                "location" : [0,0,0],
                "timestamp":capture_timestamp.isoformat()
            }

            timestamp = None
            with open(TEMP_TEXT_FILE, 'r') as file:
                lines = deque(file, maxlen=None)

            for line in reversed(lines):
                try:
                    data = ast.literal_eval(line.strip())
                    if isinstance(data, dict) and data.get('name') == name:
                        timestamp = datetime.fromisoformat(data.get('timestamp'))
                        break
                except Exception as e:
                    print(f"❌ Error parsing line: {line} -> {e}")

            if timestamp :
                time_difference = (capture_timestamp - timestamp).total_seconds()
                print(f"Time difference between last found for  is : {time_difference} ⏳")
                if time_difference > TIME_THRESHOLD:
                    x_img , y_img = get_middle_point(box,name)

                    if x_img is not None and y_img is not None :
                        z_img = get_depth(frame1, x_img, y_img)
                        camera_coordinates = pixel_to_camera_coordinates(x_img, y_img, z_img, intrinsic_matrix)
                        if camera_coordinates is not None:
                            drone_pos = np.array(drone_pos)
                            x2, y2, z2 = get_alert_pos_coordinates(camera_coordinates, drone_pos ,drone_rotation_matrix)

                            payload["location"] = [x2, y2, z2]

                    save_to_machine(payload)
                    asyncio.run(send_alert(payload,type="alert_image"))
            else :
                print(f"🔒 Saving {name} for first time!")
                x_img , y_img = get_middle_point(box,name)

                if x_img is not None and y_img is not None :
                    z_img = get_depth(frame1, x_img, y_img)
                    camera_coordinates = pixel_to_camera_coordinates(x_img, y_img, z_img, intrinsic_matrix)
                    if camera_coordinates is not None:
                        drone_pos = np.array(drone_pos)
                        x2, y2, z2 = get_alert_pos_coordinates(camera_coordinates, drone_pos ,drone_rotation_matrix)

                        payload["location"] = [x2, y2, z2]

                save_to_machine(payload)
                asyncio.run(send_alert(payload,type="alert_image"))
        else:
            print("Faces do not match. Similarity:", cosine_sim)
    else:
        print("Face not detected in one of the frames.")