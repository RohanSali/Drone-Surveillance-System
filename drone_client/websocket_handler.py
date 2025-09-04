import os
import asyncio
import websockets
import json
import base64
import urllib.parse
from datetime import datetime

current_dir = os.path.dirname(os.path.abspath(__file__))
DRONE_JSON_FILE_PATH = os.path.join(current_dir, "drone_info.json")
ALERT_QUEUE_FILE = os.path.join(current_dir, "alert_queue.txt")
TARGETS_FILE = os.path.join(current_dir, "capture_engine", "drone_targets.txt")
LOST_PERSON_FOLDER = os.path.join(current_dir, "inference_engine", "lost_person")
DRONE_POS_UPDATE_INTERVAL = 10 # seconds

SERVER_URL = "wss://new-server-5iyd.onrender.com"

class DroneWebSocketHandler:
    def __init__(self, drone_id: str):
        self.drone_id = drone_id
        encoded_id = urllib.parse.quote(drone_id)
        self.server_url = f"{SERVER_URL}/ws/drone/{encoded_id}"
        self.websocket = None
        self.connected = False

    async def connect(self):
        """Establish WebSocket connection"""
        while not self.connected:
            try:
                print(f"🔗 Connecting to {self.server_url} ...")
                self.websocket = await websockets.connect(self.server_url, ping_interval=20, ping_timeout=20)
                self.connected = True
                print(f"✅ Connected to WebSocket at: {self.server_url}")
            except Exception as e:
                print(f"❌ Connection failed: {e}. Retrying in 5s...")
                await asyncio.sleep(5)

    async def send_alert_from_queue(self):
        """Check alert_queue.txt and send any alerts found"""
        if not os.path.exists(ALERT_QUEUE_FILE):
            return

        try:
            with open(ALERT_QUEUE_FILE, "r") as f:
                lines = [line.strip() for line in f if line.strip()]

            if not lines:
                return  # No alerts to send

            # Clear file after reading
            open(ALERT_QUEUE_FILE, "w").close()

            for line in lines:
                try:
                    alert_data = json.loads(line)  # Expect {"type":"alert", "data": {...}}
                    alert_type = alert_data.get("type")
                    payload = alert_data.get("data", {})
                    payload["drone_id"] = self.drone_id

                    await self.websocket.send(json.dumps({
                        "type": alert_type,
                        "data": payload
                    }))
                    if alert_type=="alert":
                        print(f"✅ {alert_type} sent to server! : {payload['alert']}")
                    elif alert_type=="alert_image":
                        print(f"✅ {alert_type} sent to server! : {payload['name']}")
                    elif alert_type=="validated_alert":
                        print(f"✅ {alert_type} sent to server! : {payload['alert']}")

                except Exception as e:
                    print(f"❌ Failed to send alert from queue: {e}")

        except Exception as e:
            print(f"❌ Error reading alert queue: {e}")

    async def send_drone_position(self):
        """Continuously send drone position from JSON file"""
        while True:
            try:
                if self.connected and self.websocket:
                    if os.path.exists(DRONE_JSON_FILE_PATH):
                        with open(DRONE_JSON_FILE_PATH, "r") as f:
                            data = json.load(f)

                        position = data.get("location", {})
                        drone_id = data.get("drone_id", self.drone_id)
                        battery_status = data.get("battery status", "Unknown")
                        status = data.get("status", "Unknown")

                        message = {
                            "type": "drone_pos",
                            "data": {
                                "drone_id": drone_id,
                                "position": position,
                                "battery_status": battery_status,
                                "status": status,
                                "timestamp": datetime.utcnow().isoformat()
                            }
                        }

                        await self.websocket.send(json.dumps(message))
                        print(f"📤 Drone position sent: {message}")
            except Exception as e:
                print(f"❌ Error sending drone position: {e}")

            await asyncio.sleep(DRONE_POS_UPDATE_INTERVAL)

    async def handle_alert_image(self, data):
        """Process incoming alert_image message"""
        print("\n=== ALERT IMAGE RECEIVED ===")
        alert_data = data.get("data", {})
        name = alert_data.get("name", "Unknown")
        found = alert_data.get("found", 0)
        actual_image_blob = alert_data.get("actual_image")

        os.makedirs(LOST_PERSON_FOLDER, exist_ok=True)
        image_path = os.path.join(LOST_PERSON_FOLDER, f"{name}.jpg")

        if actual_image_blob:
            try:
                if found == 0:
                    if not os.path.exists(image_path):
                        with open(image_path, "wb") as f:
                            f.write(base64.b64decode(actual_image_blob))
                        print(f"💾 Image saved: {image_path}")
                    else:
                        print("ℹ Image already exists.")
                elif found == 1:
                    if os.path.exists(image_path):
                        os.remove(image_path)
                        print(f"🗑 Image deleted: {image_path}")
            except Exception as e:
                print(f"❌ Error handling image: {e}")
        else:
            print("⚠️ No image data received")
        print("=" * 50)

    async def handle_target(self, data):
        """Process incoming target message"""
        try:
            with open(TARGETS_FILE, "a") as f:
                f.write(json.dumps(data.get("data", {})) + "\n")
            print(f"🎯 Target saved to {TARGETS_FILE}: {data}")
        except Exception as e:
            print(f"❌ Error saving target: {e}")

    async def listen(self):
        """Listen to server messages"""
        try:
            async for message in self.websocket:
                try:
                    data = json.loads(message)
                    msg_type = data.get("type")

                    if msg_type == "alert_image":
                        await self.handle_alert_image(data)
                    elif msg_type == "target_pos":
                        await self.handle_target(data)
                    elif msg_type == "connection_established":
                        print("✅ Connection established with server")
                    else:
                        print(f"ℹ Unknown message type: {msg_type} | {data}")

                except json.JSONDecodeError:
                    print(f"❌ Invalid JSON received: {message}")
        except websockets.exceptions.ConnectionClosed:
            print("❌ Connection closed. Reconnecting...")
            self.connected = False
        except Exception as e:
            print(f"❌ Listen error: {e}")
            self.connected = False

    async def run(self):
        """Run main loop"""
        drone_pos_task = None
        while True:
            if not self.connected:
                await self.connect()

            if drone_pos_task is None or drone_pos_task.done():
                drone_pos_task = asyncio.create_task(self.send_drone_position())

            send_task = asyncio.create_task(self.send_alert_from_queue())
            listen_task = asyncio.create_task(self.listen())

            done, pending = await asyncio.wait(
                [send_task, listen_task],
                return_when=asyncio.FIRST_COMPLETED
            )

            for task in pending:
                task.cancel()

            await asyncio.sleep(1)  # Small delay before retry


async def main():
    # Load drone ID
    drone_id = "drone_001"
    if os.path.exists(DRONE_JSON_FILE_PATH):
        try:
            with open(DRONE_JSON_FILE_PATH, "r") as f:
                drone_id = json.load(f)["drone_id"]
        except Exception as e:
            print(f"❌ Error reading drone_info.json: {e}")

    handler = DroneWebSocketHandler(drone_id)
    await handler.run()

if __name__ == "__main__":
    asyncio.run(main())