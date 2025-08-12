# 🔧 GOOGLE OAUTH2 FIX GUIDE - Exact Solution

## ❌ Current Problem
You're getting this error: **"Google Credentials Configuration Issue"** because your Google Cloud Console OAuth2 Client is configured as **"Web application"** instead of **"Desktop application"**.

## ✅ EXACT SOLUTION STEPS

### Step 1: Delete Current OAuth2 Client in Google Console
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Navigate to **APIs & Services** → **Credentials**
3. Find your current OAuth 2.0 Client ID
4. **DELETE IT** (click the trash icon)

### Step 2: Create New Desktop Application OAuth2 Client
1. Click **"+ CREATE CREDENTIALS"**
2. Select **"OAuth 2.0 Client IDs"**
3. **IMPORTANT**: Choose **"Desktop application"** (NOT Web application!)
4. Give it a name like "Drone Surveillance Desktop"
5. Click **"Create"**

### Step 3: Configure Redirect URIs
1. After creation, click on the newly created OAuth2 client
2. In the **"Authorized redirect URIs"** section, add:
   - `http://localhost`
   - `http://localhost:8080`
3. Click **"SAVE"**

### Step 4: Download New Credentials
1. Click the download button (↓) next to your new OAuth2 client
2. This will download a JSON file
3. The JSON should look like this:
```json
{
  "installed": {
    "client_id": "YOUR_NEW_CLIENT_ID.apps.googleusercontent.com",
    "project_id": "your-project-id",
    "auth_uri": "https://accounts.google.com/o/oauth2/auth",
    "token_uri": "https://oauth2.googleapis.com/token",
    "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
    "client_secret": "GOCSPX-YOUR_NEW_CLIENT_SECRET",
    "redirect_uris": ["http://localhost"]
  }
}
```

### Step 5: Update Your Configuration Files
1. Copy the `client_id` and `client_secret` from the downloaded JSON
2. Replace the values in both files:

**Update `google_credentials.json`:**
```json
{
  "installed": {
    "client_id": "YOUR_NEW_CLIENT_ID.apps.googleusercontent.com",
    "client_secret": "GOCSPX-YOUR_NEW_CLIENT_SECRET",
    "project_id": "t-dispatcher-444519-t4",
    "auth_uri": "https://accounts.google.com/o/oauth2/auth",
    "token_uri": "https://oauth2.googleapis.com/token",
    "auth_provider_x509_cert_url": "https://www.googleapis.com/oauth2/v1/certs",
    "redirect_uris": [
      "http://localhost:8080",
      "http://localhost"
    ]
  }
}
```

**Update `appsettings.json`:**
```json
{
  "Google": {
    "ClientId": "YOUR_NEW_CLIENT_ID.apps.googleusercontent.com",
    "ClientSecret": "GOCSPX-YOUR_NEW_CLIENT_SECRET",
    "RedirectUri": ["http://localhost", "http://localhost:8080"]
  }
}
```

### Step 6: Enable Required APIs
1. Go to **APIs & Services** → **Library**
2. Search and enable these APIs:
   - **Google OAuth2 API**
   - **People API** (for profile information)

## 🎯 Why This Fixes The Issue

The error occurs because:
1. **"Web" vs "Desktop"**: Your current OAuth2 client is "Web application" type, but your app code expects "Desktop application" type
2. **Redirect URI mismatch**: Web applications use different redirect URI patterns than desktop applications
3. **Token flow difference**: Desktop applications use the "installed" flow, while web applications use the "web" flow

## ✅ After Following These Steps

1. **No more configuration popup**: The error dialog will disappear
2. **Google Sign-in will work**: The OAuth2 flow will complete successfully
3. **User authentication**: You'll be properly signed in with your Google account

## 🧪 Test Your Fix

1. Build and run your application:
   ```
   dotnet run
   ```
2. Click **"Sign in with Gmail Account"**
3. Browser opens → Grant permissions → You're signed in!

## ⚠️ Important Notes

- **Desktop vs Web**: This is the most common OAuth2 configuration mistake
- **Client ID Format**: Must end with `.apps.googleusercontent.com`
- **Client Secret Format**: Must start with `GOCSPX-`
- **Both files**: Keep both `appsettings.json` and `google_credentials.json` in sync

## 🆘 If It Still Doesn't Work

1. **Double-check application type**: Ensure it says "Desktop application" in Google Console
2. **Verify redirect URIs**: Both `http://localhost` and `http://localhost:8080` must be added
3. **Check APIs**: Google OAuth2 API must be enabled
4. **Use fallback**: Microsoft Sign-in or Guest mode still work while fixing Google

---

**Following these exact steps will fix your Google OAuth2 authentication issue!**
