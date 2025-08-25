# Artifacto 🚀

Artifacto is a simple, self-hosted solution for storing and managing versioned binary artifacts. It's designed for software teams who need a centralized place to store, organize, and retrieve their build artifacts, installers, releases, and other binary files.

## 🎯 What Problems Does Artifacto Solve?

- **Scattered Artifacts** 🗂️: Stop losing track of where your builds and releases are stored
- **Version Confusion** 🔢: Easily manage and access different versions of your software artifacts
- **Team Collaboration** 🤝: Provide a centralized location for your team to access build outputs
- **Release Management** 📦: Organize artifacts by project and maintain historical versions
- **Build Pipeline Integration** 🛠️: Use the REST API to integrate with your CI/CD pipelines

## 🌟 What makes Artifacto different from other solutions?

There are many different binary artifact storage solutions available, but they are often difficult to setup or use. Artifacto is designed to be as simple as possible to deploy and use. It is designed for personal developers or organizations as an internal tool where security concerns are less problematic.

## 🔒 Security Considerations

This tool is not suitable for teams that require high security around their binary artifacts or for distributing artifacts publically. Artifacto assumes you can trust the people using the tool. There are no user accounts or permission structures (... yet). Anyone can upload or delete artifacts and projects.

## 📋 Prerequisites

- [Docker](https://www.docker.com/get-started) 🐳 - That's it!

## ⚡ Quick Start

### 🐳 Option 1: Using Docker

1. Run the application using Docker:
  ```bash
  docker run -d \
    -p 80:80 \
    -p 443:443 \
    -v artifacto-data:/data \
    -v artifacto-certs:/etc/ssl/certs \
    ghcr.io/rbarkoch/artifacto:latest
  ```
  This will start Artifacto and make the web interface available at [http://localhost:80](http://localhost:80) or [https://localhost:443](http://localhost:443).

### 🧩 Option 2: Using Docker Compose
1. Create a `docker-compose.yml` file:
   ```yaml
   version: '3.8'
   services:
     artifacto:
       image: ghcr.io/rbarkoch/artifacto:latest
       ports:
         - "80:80"
         - "443:443"
       volumes:
         - artifacto-volume:/data
         - artifacto-certs:/etc/ssl/certs
   ```

2. Run:
   ```bash
   docker-compose up -d
   ```

### 💾 Persistent Storage 

Artifacto stores all uploaded artifacts and metadata in the `/data` directory inside the container. Make sure to use a Docker volume or bind mount for `/data` to ensure your artifacts persist across container restarts or upgrades.

Artifacto will automatically create self-signed certificates in the `/etc/ssl/certs` directory unless certificates are already available. To use your own certificates, simply mount a directory to `/etc/ssl/certs` with a `cert.crt` and `cert.key` file.

## 📦 How to Use Artifacto

### 🏗️ Creating Projects

1. Navigate to the web interface
2. Click "Create Project"
3. Provide a unique ID (lowercase letters, numbers, dashes only)
4. Add a descriptive name and optional description

**Via API:**

You can create a new project using a POST request to the `/api/projects` endpoint. Example using `curl`:

```bash
curl -X POST "http://localhost:80/api/projects" \
  -H "Content-Type: application/json" \
  -d '{
    "id": "myproject",
    "name": "My Project",
    "description": "Project for storing build artifacts"
  }'
```

Replace `"id"`, `"name"`, and `"description"` with your desired values. The `id` must use lowercase letters, numbers, and dashes only.

### ⬆️ Uploading Artifacts

**Via Web Interface:**
1. Select your project
2. Click "Upload Artifact"
3. Choose your file and provide version information
4. Add an optional description

**Via API:**
```bash
curl -X POST "http://localhost:80/api/projects/{project-id}/artifacts" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@your-artifact.zip" \
  -F "version=1.0.0" \
  -F "description=Release build"
```

### ⬇️ Downloading Artifacts

**Via Web Interface:**
- Browse projects and click download on any artifact

**Via API:**
```bash
curl -O "http://localhost:80/api/projects/{project-id}/artifacts/{version}/download"
```

## 📄 License

This project is licensed under the GNU General Public License v3.0 (GPL-3.0). See the [LICENSE](LICENSE) file for the full license text.