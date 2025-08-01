# Virtual Agent Minimal Demo

This project is a minimal demonstration of a virtual agent powered by local AI services, all integrated within a Unity client. It creates an interactive conversational experience by combining real-time Speech-to-Text (STT), a Large Language Model (LLM), and Text-to-Speech (TTS).

## How It Works

The system is composed of several independent services that communicate over the network:

1.  **Speech-to-Text (STT) Server**: Captures audio from the Unity client, transcribes it to text in real-time, and sends it to the LLM.
2.  **Large Language Model (LLM) Server**: Receives the transcribed text, generates a conversational response, and passes it to the TTS server.
3.  **Text-to-Speech (TTS) Server**: Converts the LLM's text response into audible speech.
4.  **Unity Client**: Manages the user interaction, records microphone input, sends it to the STT server, and plays the synthesized audio response from the TTS server.

## Setup Instructions

You need to set up and run three separate servers before launching the Unity application.

### 1. Speech-to-Text (STT) Server

This demo uses [RealtimeSTT](https://github.com/KoljaB/RealtimeSTT) for transcription.

1.  Follow the installation instructions in the `RealtimeSTT` repository.
2.  Run the Python server using the following command. This will start the server and listen for WebSocket connections for audio data and text output.

    ```bash
    # -m: model size (e.g., tiny.en)
    # -l: language (e.g., en)
    # -c: text output port
    # -d: audio input port
    python stt_server.py -m tiny.en -l en -c 8011 -d 8012
    ```

### 2. Large Language Model (LLM) Server

The LLM is served using [LlamaLib](https://github.com/undreamai/LlamaLib/releases/tag/v1.2.5), a server compatible with the `llama.cpp` ecosystem.

1.  Download from the LlamaLib releases.
2.  Download the GGUF model file you wish to use (e.g., Gemma-3 or Qwen-3).
3.  Place the server executable and the model file in the same directory.
4.  Open a terminal and run one of the following commands, depending on your chosen model.

**For Gemma-3 1B:**
```bash
.\undreamai_server.exe -m "gemma-3-1b-it-Q4_K_M.gguf" -c 8192 -b 512 --log-disable -np 1 -ngl 33 --template "gemma" --port 13333 --host 0.0.0.0
```

**For Qwen-3 4B:**
```bash
.\undreamai_server.exe -m "Qwen3-4B-Q4_K_M.gguf" -c 8192 -b 512 --log-disable -np 1 -ngl 33 --template "qwen3" --port 13333 --host 0.0.0.0
```

*   **`--ngl 33`**: This flag offloads 33 layers to the GPU. Adjust this number based on your GPU's VRAM. If you encounter errors, try reducing it.
*   **`--host 0.0.0.0`**: This allows the server to be accessible from other devices on your network.

### 3. Text-to-Speech (TTS) Server

The TTS service is provided by [Kokoro-FastAPI](https://github.com/remsky/Kokoro-FastAPI), which runs in a Docker container.

1.  Follow the instructions in its `README.md` to build and run the Docker container. This will expose the TTS API on your local machine.

### 4. Unity Project Setup

Once all servers are running, configure the Unity client.

1.  Open the project in Unity.
2.  In the Hierarchy, find the GameObjects responsible for handling STT, LLM, and TTS (e.g., `STT Handler`, `LLM Handler`, `TTS Handler`).
3.  In the Inspector for each of these objects, update the **IP Address** field to match the IP of the machine running the corresponding server. If all servers are on the same machine as Unity, you can use `localhost`.
4.  Select the `STT Handler` GameObject. In the Inspector, choose the correct **Microphone** you want to use from the dropdown list.

## Running the Demo

1.  Ensure all three backend servers (STT, LLM, TTS) are running without errors.
2.  In the Unity Editor, open the main scene.
3.  Enter Play Mode.
4.  Click the "Start Conversation" button in the UI.
5.  Begin speaking. You should see your transcribed text and hear the agent's response.
