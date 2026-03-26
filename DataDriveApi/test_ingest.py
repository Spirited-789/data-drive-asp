import requests

# 1. Login
res = requests.post("http://localhost:5268/auth/login", json={"email":"test@example.com", "password":"test123"})
token = res.json().get("accessToken")
print("Token:", token)

# 2. Ingest
res2 = requests.post(
    "http://localhost:5268/ingest", 
    json={"url": "https://api.coingecko.com/api/v3/coins/markets?vs_currency=usd&order=market_cap_desc&per_page=10&page=1"},
    headers={"Authorization": f"Bearer {token}"}
)
print("Status:", res2.status_code)
print("Response:", res2.text)
