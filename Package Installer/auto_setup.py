import os
import sys
import subprocess
import platform
import datetime

# ===== BOOTSTRAP PHASE =====
print("🔄 Bootstrapping environment (installing core tools)...")
subprocess.run([sys.executable, "-m", "pip", "install", "--upgrade", "pip", "setuptools", "wheel"], check=False)
subprocess.run([sys.executable, "-m", "pip", "install", "--upgrade", "requests"], check=False)

import requests  # Safe to import after bootstrap

# ===== CONFIGURATION =====
GITHUB_USER = "868Rahul"
GITHUB_REPO = "Downloadable_packages"
BRANCH = "main"

MUST_HAVE_FILES = [
    "drone_client/inference_engine/runner.py",
    "drone_client/inference_engine/result_saver.py",
    "drone_client/drone_info.json",
    "drone_client/websocket_handler.py",
    "drone_client/models/"
]

OPTIONAL_FILES = [
    "drone_client/inference_engine/alert_inference.py"
]

REQUIREMENTS_FILE = "requirements.txt"

LOG_FILE = "setup_errors.log"

# ===== UTILS =====
def log_error(message):
    """Log errors to setup_errors.log with timestamp."""
    with open(LOG_FILE, "a") as f:
        f.write(f"[{datetime.datetime.now()}] {message}\n")


def run_cmd(cmd):
    """Run shell command and stream output."""
    print(f"\n>>> Running: {cmd}\n")
    result = subprocess.run(cmd, shell=True)
    if result.returncode != 0:
        log_error(f"Command failed: {cmd}")
        sys.exit(result.returncode)


def download_file_from_github(file_path, base_client):
    """
    Download file from GitHub while preserving folder structure.
    If file_path = "drone_client/inference_engine/runner.py",
    and base_client = "drone_client", cwd is already inside drone_client.
    """
    url = f"https://raw.githubusercontent.com/{GITHUB_USER}/{GITHUB_REPO}/{BRANCH}/{file_path}"
    rel_path = os.path.relpath(file_path, base_client)  # drop top-level client folder
    local_dir = os.path.dirname(rel_path)

    if local_dir:
        os.makedirs(local_dir, exist_ok=True)

    local_filename = os.path.join(local_dir, os.path.basename(file_path))

    print(f"⬇ Downloading {rel_path}...")
    try:
        r = requests.get(url)
        if r.status_code == 200:
            with open(local_filename, "wb") as f:
                f.write(r.content)
            print(f"✔ {rel_path} downloaded.")
        else:
            log_error(f"Failed to download {file_path} | Status code: {r.status_code}")
    except Exception as e:
        log_error(f"Error downloading {file_path}: {e}")


def download_folder_from_github(folder_path, base_client):
    """
    Download all files from a folder.
    Uses GitHub API to list directory contents.
    """
    api_url = f"https://api.github.com/repos/{GITHUB_USER}/{GITHUB_REPO}/contents/{folder_path}?ref={BRANCH}"
    try:
        r = requests.get(api_url)
        if r.status_code == 200:
            items = r.json()
            for item in items:
                if item["type"] == "file":
                    download_file_from_github(item["path"], base_client)
                elif item["type"] == "dir":
                    download_folder_from_github(item["path"], base_client)
        else:
            log_error(f"Failed to list folder {folder_path} | Status code: {r.status_code}")
    except Exception as e:
        log_error(f"Error fetching folder {folder_path}: {e}")


def detect_environment():
    """Detect whether running inside Conda, venv, or system Python."""
    env_type = "System Python"
    env_path = sys.prefix

    if "CONDA_DEFAULT_ENV" in os.environ:
        env_type = f"Conda (env: {os.environ['CONDA_DEFAULT_ENV']})"
        env_path = os.environ.get("CONDA_PREFIX", sys.prefix)
    elif hasattr(sys, "real_prefix") or (hasattr(sys, "base_prefix") and sys.base_prefix != sys.prefix):
        env_type = "Virtualenv (venv)"
        env_path = sys.prefix

    return env_type, env_path


# ===== MAIN FLOW =====
def main():
    env_type, env_path = detect_environment()
    py_version = f"{sys.version_info.major}.{sys.version_info.minor}.{sys.version_info.micro}"

    print("🔍 Environment Details:")
    print(f"   • Python version : {py_version}")
    print(f"   • Environment    : {env_type}")
    print(f"   • Environment path: {env_path}")
    print(f"   • Platform       : {platform.system()} {platform.release()} ({platform.machine()})")

    choice = input("\n❓ Do you want to proceed with setup? (y/n): ").strip().lower()
    if choice != "y":
        print("❌ Setup aborted by user.")
        sys.exit(0)

    if py_version != "3.8.20":
        confirm = input("⚠️ Python != 3.8.20 detected. Continue anyway? (y/n): ").strip().lower()
        if confirm != "y":
            print("❌ Setup aborted (Python version check).")
            sys.exit(0)

    # Detect client type from file paths
    all_paths = MUST_HAVE_FILES + OPTIONAL_FILES
    clients = {p.split("/")[0] for p in all_paths}
    if len(clients) != 1:
        print("❌ Error: Multiple clients detected in configuration. Aborting.")
        sys.exit(1)

    base_client = list(clients)[0]
    print(f"\n📌 Setting up client: {base_client}")
    confirm = input("Do you want to continue? (y/n): ").strip().lower()
    if confirm != "y":
        print("❌ Setup aborted.")
        sys.exit(0)

    # Step 1: Download must-have files
    print("\n📂 Downloading must-have files...")
    for file_path in MUST_HAVE_FILES:
        if file_path.endswith("/"):
            download_folder_from_github(file_path, base_client)
        else:
            download_file_from_github(file_path, base_client)

    # Step 2: Ask for optional files/folders
    print("\n📂 Optional files/folders:")
    for file_path in OPTIONAL_FILES:
        choice = input(f"Do you want {file_path}? (y/n): ").strip().lower()
        if choice == "y":
            if file_path.endswith("/"):
                download_folder_from_github(file_path, base_client)
            else:
                download_file_from_github(file_path, base_client)

    # Step 3: Install base requirements
    download_file_from_github(REQUIREMENTS_FILE, base_client="")  # requirements.txt is at repo root
    if os.path.exists("requirements.txt"):
        run_cmd(f"{sys.executable} -m pip install -r requirements.txt --no-deps")
    else:
        print("❌ requirements.txt not found.")
        sys.exit(1)

    # Step 4: Install PyTorch (CUDA 11.8 build)
    run_cmd(
        f"{sys.executable} -m pip install torch==2.4.1+cu118 torchvision==0.19.1+cu118 "
        f"torchaudio==2.4.1+cu118 --index-url https://download.pytorch.org/whl/cu118"
    )

    # Step 5: Install TensorFlow
    run_cmd(f"{sys.executable} -m pip install tensorflow==2.10.1")

    print("\n✅ Setup completed successfully! Check 'setup_errors.log' for any errors.")


if __name__ == "__main__":
    main()
