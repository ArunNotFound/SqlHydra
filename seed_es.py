import urllib.request
import json

data = [
    { "index": { "_index": "movies", "_id": "1" } },
    { "Id": "1", "Title": "Inception", "Year": 2010 },
    { "index": { "_index": "movies", "_id": "2" } },
    { "Id": "2", "Title": "Interstellar", "Year": 2014 },
    { "index": { "_index": "movies", "_id": "3" } },
    { "Id": "3", "Title": "The Dark Knight", "Year": 2008 }
]

ndjson = ""
for line in data:
    ndjson += json.dumps(line) + "\n"

req = urllib.request.Request(
    'http://localhost:9200/_bulk',
    data=ndjson.encode('utf-8'),
    headers={'Content-Type': 'application/x-ndjson'}
)

try:
    with urllib.request.urlopen(req) as response:
        print("Status:", response.status)
        print("Response:", response.read().decode('utf-8'))
except Exception as e:
    print("Error:", e)
