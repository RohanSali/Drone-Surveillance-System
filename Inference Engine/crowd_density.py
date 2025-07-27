from inference import InferenceModel
from PIL import Image
import cv2

#API key of crowd density model fro roboflow
API_URL = "https://serverless.roboflow.com"
API_KEY = "8BOCzWsS6EiGU0IgRGnk"
PROJECT_ID = "crowd-density-ou3ne"
model = InferenceModel(
    api_key = API_URL,
    project_id = PROJECT_ID,
    model_version = 1
)


COUNT_CHART = {
    'No Person' : 0,
    'Single Person': 1,
    'Two to Four People' : 2,
    'Five to Ten People': 3,
    'Ten+ to Fifty People': 4,
    'Fifty+ to Hundred People': 5,
    'Hundered+ to Two Hundred People': 6,
    'Two Hundered+ to Five Hundred People': 7,
    'Five Hundered+ to Thousand People': 8,
    'Several Thousand People': 9,
    'Ten Thousand+' : 10
    }
DENSITY_CHART = {
    'Low Density' : [1,2,3],
    'Medium Density': [4,5,6],
    'High Desnsity' : [7,8,9]
}


def inference_crowd_density(frame,timestamp):
    img = Image.fromarray(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB))
    result = model.infer(img, model_id="crowd-density-ou3ne/1")
    print(len(result['predictions']))