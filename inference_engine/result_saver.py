import os
import json

current_dir = os.path.dirname(os.path.abspath(__file__))
project_dir = os.path.abspath(os.path.join(current_dir, '..'))

ALERT_QUEUE_FILE = os.path.join(project_dir, "alert_queue.txt")
ALERT_LOGGER = os.path.join(current_dir, 'alerts.txt')
PERSON_FOUND_LOGGER = os.path.join(current_dir, 'person_found.txt')

async def save_to_machine(payload,type="alert"):
    """Append alert data to alerts.txt or person_found.txt for local logging"""
    try:
        if type == "alert":
            with open(ALERT_LOGGER, 'a') as f:
                f.write(str(payload) + '\n')
            print(f"✅ Saved to machine: {payload['alert']}")
        elif type == "alert_image":
            with open(PERSON_FOUND_LOGGER, 'a') as f:
                f.write(str(payload) + '\n')
            print(f"✅ Saved to machine: Found - {payload['name']}")
        else:
            print("❌ Unknown type specified for saving to machine.")
    except Exception as e:
        print(f"❌ Failed to log alert: {e}")

    

async def send_alert(payload,type="alert"):
    """Append alert data to alert_queue.txt for later sending"""
    try:
        with open(ALERT_QUEUE_FILE, "a") as f:
            alert_entry = {
                "type": type,
                "data": payload
            }
            f.write(json.dumps(alert_entry) + "\n")
        if type == "alert":
            print(f"✅ Alert queued: {payload.get('alert', 'No Alert Specified')}")
        elif type == "alert_image":
            print(f"✅ Alert queued: Found - {payload.get('name', 'No Name Specified')}")
        else:
            print("❌ Unknown type specified for alert queuing.")
    except Exception as e:
        print(f"❌ Failed to queue alert: {e}")