import ast
from collections import deque
from datetime import datetime
from ultralytics import YOLO
from websocket_call import send_alert
import asyncio

MODEL_PATH = 'models/crowd_density.pt'
TEMP_TEXT_FILE = 'Inference Engine/alerts.txt'
CONFIDENCE = 0.25 #confidence threshold
TIME_THRESHOLD = 60 #sec
# URL = "https://web-production-190fc.up.railway.app/api/alerts"
# HEADERS = {
#     "Content-Type": "application/json",
#     "User-Agent": "PythonScript/1.0"
# }

model = YOLO(MODEL_PATH) 

COUNT_CHART = {
    0 :'No Person',
    1 : 'Single Person',
    2 : 'Two to Four People',
    3 : 'Five to Ten People',
    4 : 'Ten+ to Fifty People',
    5 : 'Fifty+ to Hundred People',
    6 : 'Hundered+ to Two Hundred People',
    7 : 'Two Hundered+ to Five Hundred People',
    8 : 'Five Hundered+ to Thousand People',
    9 : 'Several Thousand People',
    10 : 'Ten Thousand+'
    }
DENSITY_CHART = {
    1 : 'Low Density',
    2 : 'Low Density',
    3 : 'Low Density',
    4 : 'Medium Density',
    5 : 'Medium Density',
    6 : 'Medium Density',
    7 : 'High Desnsity',
    8 : 'High Desnsity',
    9 : 'High Desnsity'
}

def get_idx(number_of_people):
    idx = 0
    if number_of_people <= 0:
        idx = 0
    elif number_of_people == 1 :
        idx = 1
    elif number_of_people > 1 and number_of_people <= 4 :
        idx = 2
    elif number_of_people > 4 and number_of_people <= 10 :
        idx = 3
    elif number_of_people > 10 and number_of_people <= 50 :
        idx = 4
    elif number_of_people > 50 and number_of_people <= 100 :
        idx = 5
    elif number_of_people > 100 and number_of_people <= 200 :
        idx = 6
    elif number_of_people > 200 and number_of_people <= 500 :
        idx = 7
    elif number_of_people > 500 and number_of_people <= 1000 :
        idx = 8
    elif number_of_people > 1000 and number_of_people <= 10000 :
        idx = 9
    else :
        idx = 10
    return idx

# def send_to_server(payload):
#     response = requests.post(URL,json=payload, headers=HEADERS)
#     print("Response : ",response.text)
#     # json.dumps(payload)
#     if response.ok :
#         print(f"✅ Sent to server as {payload['alert']}")

def save_to_machine(payload):
    with open(TEMP_TEXT_FILE, 'a') as f:
        f.write(str(payload) + '\n')
    print(f"✅ Saved to machine as {payload['alert']}")

def inference_crowd_density(frame,capture_timestamp):
    result = model(frame, conf=CONFIDENCE ,verbose=False)
    number_of_people = len(result[0].boxes)

    idx = get_idx(number_of_people)
    
    new_label = ""

    if idx == 0 or idx == 10 :
        new_label = "Out of range - " + COUNT_CHART[idx]
    else :
        new_label = DENSITY_CHART[idx] + " - " + COUNT_CHART[idx]

    if new_label.startswith("Out of range - "):
        pass
    else : 
        payload = {
            "alert" : new_label,
            "drone_id" : "NO DRONE",
            "alert_location" : [0,0,0],
            "image" : None,
            "image_received" : 0,
            "rl_responsed" : 0,
            "score" : 0,
            "timestamp" : capture_timestamp.isoformat()
        }

        timestamp = None

        with open(TEMP_TEXT_FILE, 'r') as file:
            lines = deque(file, maxlen=None)

        for line in reversed(lines):
            try:
                data = ast.literal_eval(line.strip())
                if isinstance(data, dict) and data.get('alert') == new_label:
                    timestamp = datetime.fromisoformat(data.get('timestamp'))
                    break
            except Exception as e:
                print(f"❌ Error parsing line: {line} -> {e}")

        if timestamp :
            time_difference = (capture_timestamp - timestamp).total_seconds()
            print(f"Time difference between last {new_label} prediction is : {time_difference} ⏳")
            if time_difference > TIME_THRESHOLD:
                save_to_machine(payload)
                asyncio.run(send_alert(payload))
                # send_to_server(payload)
        else :
            print(f"🔒 Saving {new_label} for first time!")
            save_to_machine(payload)
            asyncio.run(send_alert(payload))
            # send_to_server(payload)

    print(f"Found {number_of_people} peoples in the frame!")