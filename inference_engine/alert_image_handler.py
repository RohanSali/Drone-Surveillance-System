import asyncio
import websockets
import json
import base64
import cv2
import numpy as np
from datetime import datetime
import os
from face_recognition import compare_faces
from websocket_call import send_alert

current_dir = os.path.dirname(os.path.abspath(__file__))
JSON_FILE_PATH = os.path.join(current_dir, "drone_info.json")

with open(JSON_FILE_PATH, 'r') as f:
    data = json.load(f)

URI = "wss://web-production-190fc.up.railway.app/ws/drone/" + data['drone_id']

def save_base64_image(base64_string, filename):
    """Save base64 image to file"""
    try:
        # Decode base64 string
        image_data = base64.b64decode(base64_string)
        
        # Convert to numpy array
        nparr = np.frombuffer(image_data, np.uint8)
        img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
        
        if img is not None:
            # Save the image
            cv2.imwrite(filename, img)
            print(f"✅ Image saved successfully to {filename}")
            return True
        else:
            print(f"❌ Failed to decode image")
            return False
    except Exception as e:
        print(f"❌ Error saving image: {e}")
        return False

def process_alert_image(actual_image_base64, name="Lost Person"):
    """Process the received alert image and save it for face recognition"""
    try:
        # Create lost_person directory if it doesn't exist
        lost_person_dir = os.path.join(current_dir, 'lost_person')
        if not os.path.exists(lost_person_dir):
            os.makedirs(lost_person_dir)
        
        # Save the actual image
        image_path = os.path.join(lost_person_dir, 'image1.jpg')
        if save_base64_image(actual_image_base64, image_path):
            print(f"✅ Alert image saved for face recognition: {name}")
            return True
        else:
            print(f"❌ Failed to save alert image")
            return False
    except Exception as e:
        print(f"❌ Error processing alert image: {e}")
        return False

async def handle_websocket_messages():
    """Handle incoming WebSocket messages"""
    async with websockets.connect(URI) as websocket:
        print(f"🔗 Connected to WebSocket server: {URI}")
        
        async for message in websocket:
            try:
                # Parse the message
                msg_data = json.loads(message)
                msg_type = msg_data.get('type')
                
                print(f"📨 Received message type: {msg_type}")
                
                if msg_type == "alert_image":
                    # Handle alert image from application
                    data = msg_data.get('data', {})
                    actual_image = data.get('actual_image', '')
                    name = data.get('name', 'Lost Person')
                    
                    print(f"📷 Received alert image for: {name}")
                    
                    if actual_image:
                        # Process and save the image
                        if process_alert_image(actual_image, name):
                            print(f"✅ Alert image processed successfully for: {name}")
                        else:
                            print(f"❌ Failed to process alert image for: {name}")
                    else:
                        print(f"❌ No actual_image found in message")
                        
            except json.JSONDecodeError as e:
                print(f"❌ JSON decode error: {e}")
            except Exception as e:
                print(f"❌ Error handling message: {e}")

async def start_alert_image_handler():
    """Start the alert image handler"""
    print("🚀 Starting Alert Image Handler...")
    while True:
        try:
            await handle_websocket_messages()
        except websockets.exceptions.ConnectionClosed:
            print("🔌 Connection closed, attempting to reconnect...")
            await asyncio.sleep(5)
        except Exception as e:
            print(f"❌ WebSocket error: {e}")
            await asyncio.sleep(5)

if __name__ == "__main__":
    asyncio.run(start_alert_image_handler()) 