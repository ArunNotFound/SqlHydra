import urllib.request
import json

query = { "query": "SELECT * FROM movies" }
data = json.dumps(query).encode('utf-8')
req = urllib.request.Request(
    'http://localhost:9200/_sql?format=json',
    data=data,
    headers={'Content-Type': 'application/json'}
)

try:
    with urllib.request.urlopen(req) as response:
        print("Status:", response.status)
        print("Response:", response.read().decode('utf-8'))
except Exception as e:
    print("Error:", e)
