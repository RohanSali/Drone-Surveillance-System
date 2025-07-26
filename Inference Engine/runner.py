# import cv2
# from casulty_inference import inference_casulty
# from anamoly_inference import inference_anamoly
# from datetime import datetime

# cap = cv2.VideoCapture(0)
# frame_id = 0

# while True:
#     ret, frame = cap.read()
#     timestamp = datetime.now()

#     if not ret:
#         break
    
#     if frame_id % 200 == 0:
#         inference_casulty(frame,timestamp)
#         inference_anamoly(frame,timestamp)

#     frame_id += 1

#     cv2.imshow("Live Feed", frame)
#     if cv2.waitKey(1) == ord('q'):
#         break

# cap.release()
# cv2.destroyAllWindows()


import cv2
import threading
import queue
from datetime import datetime
from casulty_inference import inference_casulty
from anamoly_inference import inference_anamoly

# Define 1-slot queues for real-time inference (latest frame only)
casulty_queue = queue.Queue(maxsize=1)
anamoly_queue = queue.Queue(maxsize=1)

# Function to safely insert latest frame into a queue (replaces old)
def safe_put(q, item):
    if q.full():
        try:
            q.get_nowait()
        except queue.Empty:
            pass
    q.put(item)

# Async inference thread for casulty
def casulty_worker():
    while True:
        frame, timestamp = casulty_queue.get()
        inference_casulty(frame, timestamp)
        casulty_queue.task_done()

# Async inference thread for anomaly
def anamoly_worker():
    while True:
        frame, timestamp = anamoly_queue.get()
        inference_anamoly(frame, timestamp)
        anamoly_queue.task_done()

# Start both threads as daemons
threading.Thread(target=casulty_worker, daemon=True).start()
threading.Thread(target=anamoly_worker, daemon=True).start()

# Capture from webcam or drone feed
cap = cv2.VideoCapture(0)
frame_id = 0

while True:
    ret, frame = cap.read()
    timestamp = datetime.now()

    if not ret:
        break

    # Send to inference queues (copy frame to avoid race condition)
    safe_put(casulty_queue, (frame.copy(), timestamp))
    safe_put(anamoly_queue, (frame.copy(), timestamp))

    frame_id += 1

    # Display live feed
    cv2.imshow("Live Feed", frame)
    if cv2.waitKey(1) == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()
