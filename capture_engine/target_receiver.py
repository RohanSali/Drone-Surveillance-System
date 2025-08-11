import os
import random

base_dir = os.path.dirname(os.path.abspath(__file__))

def create_required_files_and_folders():
    person_found_file = os.path.join(base_dir,'drone_targets.txt')

    # Create the file if it doesn't exist
    if not os.path.exists(person_found_file):
        with open(person_found_file, 'w') as f:
            f.write('')  # Or write default content
        print(f"Created file: {person_found_file}")

create_required_files_and_folders()

TARGETS_FILE_PATH = os.path.join(base_dir, 'drone_targets.txt')

target = [0,0,0,0]

def get_target():
    print("Enter target [x,y,z,yaw] : ")
    temp = ['x', 'y', 'z', 'yaw']
    for i,val in enumerate(temp):
        target[i] = int(input(val + ' : '))

    payload = {
        'target_id' : random.randint(1000, 9999),
        'location' : target
    }
    with open(TARGETS_FILE_PATH, 'a') as f:
        f.write(str(payload) + '\n')

while True:
    get_target()