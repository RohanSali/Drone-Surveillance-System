import os
import random

base_dir = os.path.dirname(os.path.abspath(__file__))

def create_required_files_and_folders():
    drone_targets_file = os.path.join(base_dir,'drone_targets.txt')

    # Create the file if it doesn't exist
    if not os.path.exists(drone_targets_file):
        with open(drone_targets_file, 'w') as f:
            f.write('')  # Or write default content
        print(f"Created file: {drone_targets_file}")

create_required_files_and_folders()

TARGETS_FILE_PATH = os.path.join(base_dir, 'drone_targets.txt')

target = [0,0,0,0]

def get_target():
    print("Enter target alert_name , alert_id , [x,y,z,yaw] : ")
    temp = ['x', 'y', 'z', 'yaw']
    alert_name = input("alert_name : ")
    alert_id = input("alert_id : ")
    for i,val in enumerate(temp):
        target[i] = int(input(val + ' : '))

    payload = {
        'target_id' : random.randint(1000, 9999),
        'alert_name' : alert_name,
        'alert_id': alert_id,
        'location' : target
    }
    with open(TARGETS_FILE_PATH, 'a') as f:
        f.write(str(payload) + '\n')

while True:
    get_target()