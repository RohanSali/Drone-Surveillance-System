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

async def send_alert(payload,type="alert"):
    async with websockets.connect(URI) as websocket:
        payload['drone_id'] = data['drone_id']
        if type == "alert":
            alert = {
                "type": "alert",
                "data": payload
            }
            await websocket.send(json.dumps(alert))
            print(f"✅ {payload['alert']} Alert sent to server!")
        elif type == "alert_image":
            face_found = {
                "type": "alert_image",
                "data": payload
            }
            await websocket.send(json.dumps(face_found))
            print(f"✅ Face found for {payload['name']} alert sent to server!")

        else:
            return