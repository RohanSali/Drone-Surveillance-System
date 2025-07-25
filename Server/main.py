from fastapi import FastAPI, UploadFile, File, Form, Depends
from fastapi.security import OAuth2PasswordBearer
from models import Session, Drone, DataLog, Group
import paho.mqtt.client as mqtt

app = FastAPI()
oauth2_scheme = OAuth2PasswordBearer(tokenUrl="token")  # For security

mqtt_client = mqtt.Client()
mqtt_client.connect("localhost", 1883, 60)
mqtt_client.loop_start()

@app.post("/api/v1/groups/create/")
async def create_group(region: str, purpose: str, rl_model_instance: str, token: str = None):
    with Session() as session:
        group = Group(region=region, purpose=purpose, rl_model_instance=rl_model_instance)
        session.add(group)
        session.commit()
        return {"group_id": group.id}

@app.post("/api/v1/drones/register/")
async def register_drone(drone_id: int, location: str, purpose: str, token: str = None):
    with Session() as session:
        group = session.query(Group).filter_by(region=location.split(',')[0], purpose=purpose).first()
        if not group:
            return {"error": "No matching group found"}
        drone = Drone(id=drone_id, group_id=group.id, location=location)
        session.add(drone)
        session.commit()
    mqtt_client.publish(f"group/{group.id}/new_drone", f"Drone {drone_id} joined")
    return {"group_id": group.id}

@app.post("/api/v1/drones/data/")
async def upload_data(drone_id: int = Form(...), image: UploadFile = File(...), location: str = Form(...), score: float = Form(...), token: str = None):
    with Session() as session:
        drone = session.query(Drone).filter_by(id=drone_id).first()
        if not drone:
            return {"error": "Drone not found"}
        log = DataLog(drone_id=drone_id, image_url=image.filename, location=location, score=score)
        session.add(log)
        session.commit()
    with open(f"uploads/{image.filename}", "wb") as f:
        f.write(await image.read())
    mqtt_client.publish(f"group/{drone.group_id}/data", f"New data from {drone_id}")
    return {"status": "Data received"}

@app.post("/api/v1/drones/control/")
async def send_control(group_id: int, command: str, token: str = None):
    mqtt_client.publish(f"group/{group_id}/control", command)
    return {"status": "Command sent"}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
