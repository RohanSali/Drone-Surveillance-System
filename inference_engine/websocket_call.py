import asyncio
import websockets
import json
from datetime import datetime
import os

current_dir = os.path.dirname(os.path.abspath(__file__))
JSON_FILE_PATH = os.path.join(current_dir,"drone_info.json")

with open(JSON_FILE_PATH, 'r') as f:
    data = json.load(f)

URI = "wss://web-production-190fc.up.railway.app/ws/drone/"+data['drone_id']

payload = {
            "alert" : "Sent by Rohan",
            "drone_id" : "NO DRONE",
            "alert_location" : [0,0,0],
            "image" : None,
            "image_received" : 0,
            "rl_responsed" : 0,
            "score" : 0,
            "timestamp" : datetime.now().isoformat()
        }

async def send_alert(payload,type="alert"):
    async with websockets.connect(URI) as websocket:
        payload['drone_id'] = data['drone_id']
        if type == "alert":
            alert = {
                "type": "alert",
                "data": payload
            }
            await websocket.send(json.dumps(alert))
            print("✅ Alert sent to server!", payload['alert'])
        elif type == "face_found":
            face_found = {
                "type": "face_found",
                "data": payload
            }
            await websocket.send(json.dumps(face_found))
            print("✅ Face found alert sent to server!", payload['alert'])
        elif type == "image_received":
            # Send matched image response back to application
            image_response = {
                "type": "image_received",
                "data": payload
            }
            await websocket.send(json.dumps(image_response))
            print("✅ Image received response sent to server! Found:", payload.get('found', 0))
        else:
            return