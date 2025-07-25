import requests

url = "http://127.0.0.1:8000/api/v1/groups/create/"
headers = {
    "Authorization": "Bearer your_token_here"  # optional if token not enforced
}
data = {
    "region": "urban_zone",
    "purpose": "casualty_detection",
    "rl_model_instance": "model_v1"
}

response = requests.post(url, data=data, headers=headers)
print(response.json())
