import asyncio
import websockets
import json
import base64
import cv2
import numpy as np
from datetime import datetime
import os

# Test the WebSocket connection and message handling
async def test_websocket():
    """Test the WebSocket connection"""
    current_dir = os.path.dirname(os.path.abspath(__file__))
    JSON_FILE_PATH = os.path.join(current_dir, "drone_info.json")
    
    with open(JSON_FILE_PATH, 'r') as f:
        data = json.load(f)
    
    URI = "wss://web-production-190fc.up.railway.app/ws/drone/" + data['drone_id']
    
    print(f"🔗 Testing WebSocket connection to: {URI}")
    
    try:
        async with websockets.connect(URI) as websocket:
            print("✅ WebSocket connection successful!")
            
            # Send a test message
            test_message = {
                "type": "test",
                "data": {
                    "message": "Hello from drone",
                    "timestamp": datetime.now().isoformat()
                }
            }
            
            await websocket.send(json.dumps(test_message))
            print("✅ Test message sent!")
            
            # Wait for a response
            try:
                response = await asyncio.wait_for(websocket.recv(), timeout=5.0)
                print(f"📨 Received response: {response}")
            except asyncio.TimeoutError:
                print("⏰ No response received within 5 seconds (this is normal)")
                
    except Exception as e:
        print(f"❌ WebSocket connection failed: {e}")

if __name__ == "__main__":
    asyncio.run(test_websocket()) 