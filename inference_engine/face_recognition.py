import torch
import os
from facenet_pytorch import InceptionResnetV1, MTCNN
from PIL import Image
import cv2
import ast
import base64
from collections import deque
from datetime import datetime
from websocket_call import send_alert
import asyncio

current_dir = os.path.dirname(os.path.abspath(__file__))
project_dir = os.path.abspath(os.path.join(current_dir, '..'))

TEMP_TEXT_FILE = os.path.join(current_dir,"person_found.txt")
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
            
            # Create payload for saving to machine
            payload = {
                "found":1,
                "name":name,
                "drone_id": "No Drone",
                "actual_image": frame2_blob,
                "matched_frame" : frame1_blob,
                "location" : [0,0,0],
                "timestamp":capture_timestamp.isoformat()
            }

            # Create WebSocket response payload for application
            ws_response_payload = {
                "found": 1,
                "name": name,
                "drone_id": "drone_001",
                "actual_image": frame2_blob,
                "matched_image": frame1_blob,
                "location": [0,0,0],
                "score": str(cosine_sim),
                "timestamp": capture_timestamp.isoformat()
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
                    # Send response back to application via WebSocket
                    asyncio.run(send_alert(ws_response_payload, "image_received"))
            else :
                print(f"🔒 Saving {name} for first time!")
                save_to_machine(payload)
                # Send response back to application via WebSocket
                asyncio.run(send_alert(ws_response_payload, "image_received"))
        else:
            print("Faces do not match. Similarity:", cosine_sim)
            # Send response even when no match found
            frame1_blob = encode_frame(frame1)
            frame2_blob = encode_frame(frame2)
            
            ws_response_payload = {
                "found": 0,
                "name": name,
                "drone_id": "drone_001",
                "actual_image": frame2_blob,
                "matched_image": "",
                "location": [0,0,0],
                "score": str(cosine_sim),
                "timestamp": capture_timestamp.isoformat()
            }
            
            # Send response back to application via WebSocket
            asyncio.run(send_alert(ws_response_payload, "image_received"))
    else:
        print("Face not detected in one of the frames.")
        # Send response even when face not detected
        frame1_blob = encode_frame(frame1)
        frame2_blob = encode_frame(frame2)
        
        ws_response_payload = {
            "found": 0,
            "name": name,
            "drone_id": "drone_001",
            "actual_image": frame2_blob,
            "matched_image": "",
            "location": [0,0,0],
            "score": "0.0",
            "timestamp": capture_timestamp.isoformat()
        }
        
        # Send response back to application via WebSocket
        asyncio.run(send_alert(ws_response_payload, "image_received"))