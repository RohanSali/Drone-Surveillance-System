import os
import asyncio
import websockets
import json
import base64
import urllib.parse
from typing import Dict, Any

current_dir = os.path.dirname(os.path.abspath(__file__))
project_dir = os.path.abspath(os.path.join(current_dir, '..'))
JSON_FILE_PATH = os.path.join(current_dir, "drone_info.json")

FOLDER_PATH = os.path.join(current_dir, 'lost_person')

class DroneClient:
    def __init__(self, drone_id: str, server_url: str = "ws://web-production-190fc.up.railway.app"):
        self.drone_id = drone_id
        encoded_drone_id = urllib.parse.quote(drone_id)
        self.server_url = f"{server_url}/ws/drone/{encoded_drone_id}"
        self.websocket = None
        self.connected = False

    async def connect(self):
        try:
            print(f"🔗 Attempting to connect to: {self.server_url}")
            self.websocket = await websockets.connect(self.server_url)
            self.connected = True
            print(f"✅ Connected to WebSocket at: {self.server_url}")
            
            # Send initial connection message
            await self.websocket.send(json.dumps({
                "type": "drone_connect",
                "data": {"drone_id": self.drone_id}
            }))
            print("📤 Sent initial connection message")
            
        except Exception as e:
            print(f"❌ Connection failed: {e}")
            self.connected = False

    async def disconnect(self):
        """Disconnect from the server"""
        if self.websocket:
            await self.websocket.close()
            self.connected = False
            print(f"Drone {self.drone_id} disconnected")

    async def handle_alert_image(self, alert_image_data: Dict[str, Any]):
        """Handle alert image received from application via server"""
        print(f"\n=== ALERT IMAGE RECEIVED ===")
        print(f"Raw data: {json.dumps(alert_image_data, indent=2)[:500]}...")
        
        # Handle both possible message structures
        if "data" in alert_image_data:
            # Server format: {"type": "alert_image", "data": {...}}
            alert_data = alert_image_data.get("data", {})
            alert_image_id = alert_data.get("alert_image_id") or alert_data.get("_id") or alert_data.get("id")
            found = alert_data.get("found", 0)
            actual_image_blob = alert_data.get('actual_image')
            matched_frame_blob = alert_data.get('matched_frame')
            name = alert_data.get('name', 'Unknown')
            location = alert_data.get('location', 'Unknown')
            timestamp = alert_data.get('timestamp', 'Unknown')
        else:
            # Direct format: {"type": "alert_image", "alert_image": {...}}
            alert_image_id = alert_image_data.get("alert_image_id")
            alert_image = alert_image_data.get("alert_image", {})
            found = alert_image.get("found", 0)
            actual_image_blob = alert_image.get('actual_image')
            matched_frame_blob = alert_image.get('matched_frame')
            name = alert_image.get('name', 'Unknown')
            location = alert_image.get('location', 'Unknown')
            timestamp = alert_image.get('timestamp', 'Unknown')

        print(f"Alert Image ID: {alert_image_id}")
        print(f"Object Name: {name}")
        print(f"Location: {location}")
        print(f"Detection Status: {'Found' if found == 1 else 'Not Found'}")
        print(f"Timestamp: {timestamp}")
        
        # Create folder if it doesn't exist
        os.makedirs(FOLDER_PATH, exist_ok=True)
        
        # Process actual image
        if actual_image_blob:
            print(f"✅ Received actual image (base64 encoded, {len(actual_image_blob)} chars)")
            # actual_image_path = os.path.join(FOLDER_PATH, f"drone_{self.drone_id}_alert_{alert_image_id}_actual.jpg")
            actual_image_path = os.path.join(FOLDER_PATH, f"{name}.jpg")
            
            if found == 0:
                if not os.path.exists(actual_image_path):
                    try:
                        actual_image_bytes = base64.b64decode(actual_image_blob)
                        with open(actual_image_path, "wb") as f:
                            f.write(actual_image_bytes)
                        print(f"💾 Image saved to folder: {actual_image_path}")
                    except Exception as e:
                        print(f"❌ Error saving image: {e}")
                else:
                    print("ℹ Image already exists.")
            elif found == 1:
                if os.path.exists(actual_image_path):
                    try:
                        os.remove(actual_image_path)
                        print(f"🗑 Image removed from folder: {actual_image_path}")
                    except Exception as e:
                        print(f"❌ Error deleting image: {e}")
                else:
                    print("ℹ Image not found in folder to delete.")
        else:
            print("⚠️ No actual image data received")
        
        print("=" * 50)

    async def listen(self):
        if not self.websocket:
            return

        try:
            async for message in self.websocket:
                try:
                    print(f"📨 Received message: {message[:200]}...")  # Debug: show first 200 chars
                    data = json.loads(message)

                    message_type = data.get("type")
                    
                    if message_type == "alert_image":
                        print("🎯 Processing alert image...")
                        await self.handle_alert_image(data)
                    elif message_type == "connection_established":
                        print("✅ Connection established with server")
                    else:
                        print(f"📝 Received other message type: {message_type}")
                        print(f"Full message: {json.dumps(data, indent=2)}")
                        
                except json.JSONDecodeError:
                    print(f"❌ Invalid JSON received: {message}")
        except websockets.exceptions.ConnectionClosed:
            print("❌ Connection closed")
            self.connected = False
        except Exception as e:
            print(f"❌ Error in listen loop: {e}")
            self.connected = False

    async def run(self):
        await self.connect()
        if self.connected:
            listen_task = asyncio.create_task(self.listen())
            
            try:
                await asyncio.gather(listen_task)
            except Exception as e:
                print(f"❌ Error in run loop: {e}")
            finally:
                listen_task.cancel()

async def main():
    # Check if drone_info.json exists, otherwise use default
    drone_id = "drone_001"  # Default drone ID
    
    if os.path.exists(JSON_FILE_PATH):
        try:
            with open(JSON_FILE_PATH, "r") as f:
                drone_info = json.load(f)
            drone_id = drone_info["drone_id"]
            print(f"📋 Loaded drone_id from config: {drone_id}")
        except Exception as e:
            print(f"❌ Error reading drone_info.json: {e}")
            print(f"Using default drone_id: {drone_id}")
    else:
        print(f"❌ drone_info.json not found at: {JSON_FILE_PATH}")
        print(f"Using default drone_id: {drone_id}")

    print(f"🚁 Starting drone client with ID: {drone_id}")
    print(f"📁 Images will be saved to: {FOLDER_PATH}")
    print("=" * 50)
    
    client = DroneClient(drone_id)
    await client.run()

if __name__ == "__main__":
    asyncio.run(main())