## Build Instructions

This engine is written **100% in C#** and can be built using either the **.NET SDK (CLI)** or **Visual Studio 2022**.  
Both methods produce identical binaries.

---

### 🔧 Requirements

- **.NET SDK 10.0** 
- **Visual Studio 2022** (optional, recommended for development)  
- Supported OS: **Windows**, **Linux**, **macOS**

Check your installed .NET version:

dotnet --version

## LIBS
- JoltPhysicsSharp
- ImGui.NET
- AssimpNet
- Jint
- NAudio
- Newtonsoft.Json
- OpenTK
- StbImageSharp
- StbImageWriteSharp

## Fix: Unable to load shared library 'libdl.so' on Linux

If you get the following error when loading a model in Linux:


You can fix it by installing the required dependencies:

```bash
sudo apt update
sudo apt install libminizip1 libz-dev
```

<img width="1920" height="1009" alt="image" src="https://github.com/user-attachments/assets/816fd1f8-757a-47c8-8d53-bdb76f9f8782" />

<img width="1920" height="1009" alt="KrayonEditor_uOyl6URVfl" src="https://github.com/user-attachments/assets/3ee628f7-a0e7-47ba-9c00-dd577df25cb6" />

<img width="1917" height="1031" alt="image" src="https://github.com/user-attachments/assets/5f44469f-fb2b-41d9-b554-c632fcc54e27" />

