import torch
from facenet_pytorch import InceptionResnetV1, MTCNN
from PIL import Image
import numpy as np
import cv2
import ast
import base64
import requests
from collections import deque
from datetime import datetime

URL = "https://web-production-190fc.up.railway.app/api/alerts"
TEMP_TEXT_FILE = 'Inference Engine/person_found.txt'
TIME_THRESHOLD = 60 #sec

# Load pretrained FaceNet model
model = InceptionResnetV1(pretrained='vggface2').eval()
mtcnn = MTCNN(image_size=160, margin=0)

def get_embedding_from_frame(frame):
    # Convert to RGB PIL Image
    img = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
    face = mtcnn(img)
    if face is not None:
        return model(face.unsqueeze(0))
    else:
        return None

def encode_frame(frame):
    # Encode the frame to JPEG format
    success, encoded_image = cv2.imencode('.jpg', frame)
    if not success:
        raise ValueError("Image encoding failed")

    # Convert to base64
    base64_image = base64.b64encode(encoded_image).decode('utf-8')
    return base64_image

def send_to_server(payload):
    response = requests.post(URL,json=payload)
    # json.dumps(payload)
    if response.ok :
        print(f"✅ Sent to server as {payload['name']}")

def save_to_machine(payload):
    with open(TEMP_TEXT_FILE, 'a') as f:
        f.write(str(payload) + '\n')
    print(f"✅ Saved to machine as {payload['name']}")


def compare_faces(frame1, frame2 , name ,capture_timestamp):
    emb1 = get_embedding_from_frame(frame1)
    emb2 = get_embedding_from_frame(frame2)

    if emb1 is not None and emb2 is not None:
        cosine_sim = torch.nn.functional.cosine_similarity(emb1, emb2).item()
        threshold = 0.6
        if cosine_sim > threshold:
            print("Faces match! Similarity:", cosine_sim , "Person found at timestamp : ",capture_timestamp)

            frame1_blob = encode_frame(frame1)
            frame2_blob = encode_frame(frame2)
            payload = {
                "found":1,
                "name":name,
                "actual image": frame2_blob,
                "matched frame" : frame1_blob,
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
                    save_to_machine(payload)
                    #send_to_server(payload)
            else :
                print(f"🔒 Saving {name} for first time!")
                save_to_machine(payload)
                #send_to_server(payload)


        else:
            print("Faces do not match. Similarity:", cosine_sim)
    else:
        print("Face not detected in one of the frames.")